// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Collections.Immutable;
using System.Text.Json;
using Rulealize.Abstraction.Building;
using Rulealize.Abstraction.Node;
using Rulealize.Abstraction.Value;

namespace Rulealize.Plugin.Record
{
    /// <summary>The schema of a field holding a record.</summary>
    /// <remarks>
    /// <para>
    /// The key set is closed and declared. An open map would be less to write and much less
    /// to check: a declared key set is what lets a mistyped key be an error rather than a
    /// silent null, and what gives <c>rec.keys</c> something to answer. It is the same
    /// judgement that put <c>type.enum</c> ahead of a string with a pattern.
    /// </para>
    /// <para>
    /// Closedness is enforced where values are made, not only where they are checked:
    /// <c>rec.with</c> refuses a key this schema does not declare, so a record that started
    /// valid stays valid however a rule set rewrites it.
    /// </para>
    /// <para>
    /// Keys are written in declaration order, so a state document does not reorder itself
    /// between transitions.
    /// </para>
    /// </remarks>
    internal sealed class RecordSchemaNode(ImmutableArray<KeyValuePair<string, SchemaNode>> fields) : SchemaNode
    {
        /// <summary>Gets the declared keys, in order.</summary>
        public ImmutableArray<string> Keys { get; } = [.. fields.Select(static field => field.Key)];

        /// <inheritdoc />
        /// <remarks>
        /// A record field always holds a record. Where a whole record may be absent, declare
        /// the absence inside it — the alternative is every reader checking for null before
        /// every key.
        /// </remarks>
        public override bool IsNullable => false;

        /// <summary>Gets the schema of one key, or <see langword="null"/> when it is not declared.</summary>
        /// <param name="key">The key.</param>
        /// <returns>The schema.</returns>
        public SchemaNode? For(string key)
        {
            foreach ((string name, SchemaNode schema) in fields)
            {
                if (string.Equals(name, key, StringComparison.Ordinal))
                {
                    return schema;
                }
            }

            return null;
        }

        /// <summary><c>rec.of</c>: named fields, each with a schema of its own.</summary>
        public static SchemaNode BuildFields(INodeBuildContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            JsonElement declared = context.GetRequiredProperty("fields");
            if (declared.ValueKind != JsonValueKind.Object)
            {
                throw context.Error("fields", "must be an object mapping keys to schemas.");
            }

            ImmutableArray<KeyValuePair<string, SchemaNode>>.Builder fields =
                ImmutableArray.CreateBuilder<KeyValuePair<string, SchemaNode>>();

            foreach (JsonProperty field in declared.EnumerateObject())
            {
                fields.Add(new KeyValuePair<string, SchemaNode>(
                    field.Name,
                    context.BuildSchema(field.Value, $"fields/{field.Name}")));
            }

            return fields.Count == 0
                ? throw context.Error("fields", "must declare at least one key.")
                : new RecordSchemaNode(fields.ToImmutable());
        }

        /// <summary><c>rec.map</c>: enumerated keys sharing one schema.</summary>
        /// <remarks>
        /// Worth having beside <c>rec.of</c> because it says something that form cannot — that
        /// every value has the same type. A shogi hand is seven counters per side and reads
        /// as four lines here against fourteen there.
        /// </remarks>
        public static SchemaNode BuildMap(INodeBuildContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            ImmutableArray<string> keys = context.RequireStringArray("keys");
            if (keys.IsEmpty)
            {
                throw context.Error("keys", "must declare at least one key.");
            }

            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                if (!seen.Add(key))
                {
                    throw context.Error("keys", $"'{key}' is declared more than once.");
                }
            }

            SchemaNode value = context.RequireSchema("value");
            return new RecordSchemaNode([.. keys.Select(key => new KeyValuePair<string, SchemaNode>(key, value))]);
        }

        /// <inheritdoc />
        public override void Validate(RuleValue value, ISchemaValidationSink sink)
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(sink);

            if (value is not RecordValue record)
            {
                sink.Violation($"Expected a record but got {RuleValue.Describe(value)}.");
                return;
            }

            foreach ((string key, SchemaNode schema) in fields)
            {
                if (record.Fields.ContainsKey(key))
                {
                    schema.Validate(record[key], new KeyValidationSink(sink, key));
                }
                else
                {
                    sink.Violation(key, "is missing.");
                }
            }

            foreach (string key in record.Fields.Keys)
            {
                if (For(key) is null)
                {
                    sink.Violation(key, "is not a key this record declares.");
                }
            }
        }

        /// <inheritdoc />
        public override RuleValue ReadJson(JsonElement element, ISchemaValidationSink sink)
        {
            ArgumentNullException.ThrowIfNull(sink);

            Dictionary<string, RuleValue> values = new(fields.Length, StringComparer.Ordinal);
            if (element.ValueKind != JsonValueKind.Object)
            {
                sink.Violation("Expected an object.");
                return RuleValue.Record(values);
            }

            foreach ((string key, SchemaNode schema) in fields)
            {
                if (element.TryGetProperty(key, out JsonElement declared))
                {
                    values[key] = schema.ReadJson(declared, new KeyValidationSink(sink, key));
                }
                else
                {
                    sink.Violation(key, "is missing.");
                    values[key] = RuleValue.Null;
                }
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (For(property.Name) is null)
                {
                    sink.Violation(property.Name, "is not a key this record declares.");
                }
            }

            return RuleValue.Record(values);
        }

        /// <inheritdoc />
        public override void WriteJson(Utf8JsonWriter writer, RuleValue value)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(value);

            RecordValue record = RecordArguments.Require(value, "rec");

            writer.WriteStartObject();
            foreach ((string key, SchemaNode schema) in fields)
            {
                writer.WritePropertyName(key);
                schema.WriteJson(writer, record[key]);
            }

            writer.WriteEndObject();
        }
    }

    /// <summary>Reports a value's violations under the key they were found at.</summary>
    internal sealed class KeyValidationSink(ISchemaValidationSink inner, string key) : ISchemaValidationSink
    {
        public bool HasViolations => inner.HasViolations;

        public void Violation(string message) => inner.Violation(key, message);

        public void Violation(string relativePath, string message) => inner.Violation($"{key}/{relativePath}", message);
    }
}
