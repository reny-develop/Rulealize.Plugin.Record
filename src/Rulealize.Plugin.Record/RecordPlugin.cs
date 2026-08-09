// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using Rulealize.Abstraction.Plugin;

namespace Rulealize.Plugin.Record
{
    /// <summary>
    /// Records in the state, over the <c>rec</c> namespace.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The state schema is a flat map of field names, so anything shaped like a map has to be
    /// spread across one field per key. A shogi hand — seven kinds for each of two players —
    /// costs fourteen fields, fourteen reads selected by a two-level match, and twenty-eight
    /// effects, because a <c>state.set</c> path is a literal and there is no way to say
    /// "the counter for this kind".
    /// </para>
    /// <para>
    /// The path stays a literal. What this plugin adds is the other half of the seam a grid
    /// already uses: the field is named literally and the inside is reached by the vocabulary
    /// of whoever owns it. <c>rec.at</c> reads a computed key, <c>rec.update</c> writes one,
    /// and the three benefits of literal paths — every path checkable before anything runs,
    /// a document that says which fields an input writes, validation that need not wait for
    /// evaluation — are all still there.
    /// </para>
    /// <para>
    /// Lists needed no plugin of their own. A list field holds a sequence, and the sequence
    /// plugin can already read one; a record had nothing.
    /// </para>
    /// </remarks>
    public sealed class RecordPlugin : IRulealizePlugin
    {
        /// <inheritdoc />
        public PluginManifest Manifest { get; } =
            new("Rulealize.Plugin.Record", new Version(1, 0, 0), "rec");

        /// <inheritdoc />
        public void Register(IPluginRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            registry.AddSchema("of", RecordSchemaNode.BuildFields);
            registry.AddSchema("map", RecordSchemaNode.BuildMap);
            registry.AddExpression("at", AtNode.Build);
            registry.AddExpression("has", HasNode.Build);
            registry.AddExpression("with", WithNode.Build);
            registry.AddExpression("keys", KeysNode.Build);
            registry.AddEffect("set", SetNode.Build);
            registry.AddEffect("update", UpdateNode.Build);
        }
    }
}
