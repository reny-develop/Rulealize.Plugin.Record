// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using Rulealize.Abstraction;
using Rulealize.Abstraction.Building;
using Rulealize.Abstraction.Evaluation;
using Rulealize.Abstraction.Node;
using Rulealize.Abstraction.Value;

namespace Rulealize.Plugin.Record
{
    /// <summary>Turns the values a node was handed into records.</summary>
    internal static class RecordArguments
    {
        public static RecordValue Require(RuleValue value, string origin) =>
            value as RecordValue
            ?? throw new RuleEvaluationException(origin, $"Expected a record but got {RuleValue.Describe(value)}.");

        public static string RequireKey(RuleValue value, string origin) =>
            value is TextValue text
                ? text.Value
                : throw new RuleEvaluationException(
                    origin,
                    $"A record key is text, but this is {RuleValue.Describe(value)}.");
    }

    /// <summary>The value one key holds.</summary>
    /// <remarks>
    /// <para>
    /// A key the record does not have is an evaluation error, and this is where the plugin
    /// parts company with <c>grid.at</c>. A board has squares that legitimately do not exist,
    /// so reading off the edge has a real answer and Reversi's capture rule depends on
    /// getting it. A record's keys are all declared; asking for one that is not there is a
    /// mistake, and no rule anywhere is relying on it being quiet.
    /// </para>
    /// <para>
    /// A null record still reads as null, the way a null tuple does. That is a different
    /// question — there is no record to ask — and it keeps null propagating through a chain
    /// of lookups.
    /// </para>
    /// </remarks>
    internal sealed class AtNode(ExpressionNode record, ExpressionNode key) : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return new AtNode(context.RequireExpression("record"), context.RequireExpression("key"));
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            RuleValue value = record.Evaluate(context);
            if (value.IsNull)
            {
                return RuleValue.Null;
            }

            RecordValue subject = RecordArguments.Require(value, "rec.at.record");
            string name = RecordArguments.RequireKey(key.Evaluate(context), "rec.at.key");

            return subject.Fields.TryGetValue(name, out RuleValue? held)
                ? held
                : throw new RuleEvaluationException("rec.at.key", $"This record has no key '{name}'.");
        }
    }

    /// <summary>Whether a record has a key.</summary>
    /// <remarks>
    /// The way to ask before asking. <c>rec.at</c> faults on a key the record does not have,
    /// which is the right answer for a mistake and no answer at all for a question — shogi
    /// puts a captured piece into a hand and has to find out first whether the thing it took
    /// is one a hand holds. Without this the rule set would keep a second list of the keys
    /// beside the schema, for the two to drift apart later.
    /// </remarks>
    internal sealed class HasNode(ExpressionNode record, ExpressionNode key) : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return new HasNode(context.RequireExpression("record"), context.RequireExpression("key"));
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            RuleValue value = record.Evaluate(context);
            if (value.IsNull)
            {
                return RuleValue.False;
            }

            RecordValue subject = RecordArguments.Require(value, "rec.has.record");
            string name = RecordArguments.RequireKey(key.Evaluate(context), "rec.has.key");

            return RuleValue.Boolean(subject.Fields.ContainsKey(name));
        }
    }

    /// <summary>A record with one key replaced, as a value.</summary>
    /// <remarks>
    /// <para>
    /// Writes nothing. It is what a guard uses to ask about the state a move would produce,
    /// the same part <c>grid.with</c> plays for a board, and what <c>state.set</c> is given
    /// when a whole field is being replaced at once.
    /// </para>
    /// <para>
    /// A key the record does not already have is refused. That is what keeps a record inside
    /// its schema no matter how a rule set rewrites it: the key set can only ever be the one
    /// the record was read with.
    /// </para>
    /// </remarks>
    internal sealed class WithNode(ExpressionNode record, ExpressionNode key, ExpressionNode value) : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return new WithNode(
                context.RequireExpression("record"),
                context.RequireExpression("key"),
                context.RequireExpression("value"));
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            RecordValue subject = RecordArguments.Require(record.Evaluate(context), "rec.with.record");
            string name = RecordArguments.RequireKey(key.Evaluate(context), "rec.with.key");

            return Replace(subject, name, value.Evaluate(context), "rec.with.key");
        }

        /// <summary>Returns a record with one existing key given a new value.</summary>
        /// <param name="subject">The record.</param>
        /// <param name="key">The key, which must already be present.</param>
        /// <param name="value">The new value.</param>
        /// <param name="origin">Where this came from, for the error message.</param>
        /// <returns>A new record. The one passed in is unchanged.</returns>
        public static RecordValue Replace(RecordValue subject, string key, RuleValue value, string origin)
        {
            ArgumentNullException.ThrowIfNull(subject);

            if (!subject.Fields.ContainsKey(key))
            {
                throw new RuleEvaluationException(origin, $"This record has no key '{key}'.");
            }

            Dictionary<string, RuleValue> fields = new(subject.Fields, StringComparer.Ordinal) { [key] = value };
            return new RecordValue(fields);
        }
    }

    /// <summary>The keys a record has, as a sequence of text.</summary>
    /// <remarks>
    /// <para>
    /// The way into a record for everything the sequence plugin can already do. A shogi rule
    /// set asks which pieces are in hand by filtering these, where before it wrote the seven
    /// kinds out and hoped they matched the schema.
    /// </para>
    /// <para>
    /// Ordinal order, not declaration order. A record is a value and may have been built by
    /// a literal that no schema ever saw, so declaration order is not always a thing that
    /// exists; sorting is the only order that always does. Results should not depend on which
    /// run they were, and <c>GetValidInputs</c> enumerating a domain built from these would.
    /// </para>
    /// </remarks>
    internal sealed class KeysNode(ExpressionNode record) : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return new KeysNode(context.RequireExpression("of"));
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            RecordValue subject = RecordArguments.Require(record.Evaluate(context), "rec.keys.of");
            List<RuleValue> keys = [.. subject.Fields.Keys.Order(StringComparer.Ordinal).Select(RuleValue.Text)];
            return RuleValue.Sequence(keys);
        }
    }
}
