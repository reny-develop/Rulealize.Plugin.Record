// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using Rulealize.Abstraction.Building;
using Rulealize.Abstraction.Evaluation;
using Rulealize.Abstraction.Node;
using Rulealize.Abstraction.Value;

namespace Rulealize.Plugin.Record
{
    /// <summary>Writes one key of a record field.</summary>
    /// <remarks>
    /// <para>
    /// An effect node, so it appears only in an input's <c>effects</c>.
    /// </para>
    /// <para>
    /// This is the operation the state plugin cannot provide, and the reason is worth being
    /// clear about. A <c>state.set</c> path is a literal, deliberately — it is what makes
    /// every path checkable before anything runs and what a document says about which fields
    /// an input touches. A key, though, is usually computed: shogi adds to the hand of
    /// whoever is to move, a piece whose kind it worked out from the board. Naming the field
    /// literally and the key by expression keeps both.
    /// </para>
    /// <para>
    /// The record is read from the draft rather than the snapshot, so two effects writing
    /// different keys of one record add up instead of the second undoing the first. The
    /// expressions inside them still read the state as it was.
    /// </para>
    /// </remarks>
    internal sealed class SetNode(StatePath field, ExpressionNode key, ExpressionNode value) : EffectNode
    {
        public static EffectNode Build(INodeBuildContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return new SetNode(
                TargetRecord.Resolve(context),
                context.RequireExpression("key"),
                context.RequireExpression("value"));
        }

        public override void Apply(IEvaluationContext context, IStateDraft draft)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(draft);

            RecordValue current = RecordArguments.Require(draft.Get(field), "rec.set.target");
            string name = RecordArguments.RequireKey(key.Evaluate(context), "rec.set.key");

            draft.Set(field, WithNode.Replace(current, name, value.Evaluate(context), "rec.set.key"));
        }
    }

    /// <summary>Rewrites one key of a record field from what it already holds.</summary>
    /// <remarks>
    /// The keyed counterpart of <c>state.update</c>. Counting is the case it exists for —
    /// a hand gains a piece, a tally goes up — and writing that with <c>rec.set</c> would
    /// mean naming the key twice and reading it back through <c>rec.at</c> in between.
    /// </remarks>
    internal sealed class UpdateNode(StatePath field, ExpressionNode key, LocalSlot current, ExpressionNode value)
        : EffectNode
    {
        public static EffectNode Build(INodeBuildContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            StatePath field = TargetRecord.Resolve(context);
            ExpressionNode key = context.RequireExpression("key");

            using (context.Scope.BeginScope())
            {
                LocalSlot slot = context.Scope.Declare(context.RequireString("as"));
                return new UpdateNode(field, key, slot, context.RequireExpression("value"));
            }
        }

        public override void Apply(IEvaluationContext context, IStateDraft draft)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(draft);

            RecordValue record = RecordArguments.Require(draft.Get(field), "rec.update.target");
            string name = RecordArguments.RequireKey(key.Evaluate(context), "rec.update.key");

            if (!record.Fields.TryGetValue(name, out RuleValue? held))
            {
                throw new Abstraction.RuleEvaluationException(
                    "rec.update.key",
                    $"This record has no key '{name}'.");
            }

            RuleValue replacement = value.Evaluate(context.Bind(current, held));
            draft.Set(field, WithNode.Replace(record, name, replacement, "rec.update.key"));
        }
    }

    /// <summary>Resolves the <c>target</c> of a record-writing effect.</summary>
    /// <remarks>
    /// Written <c>"$hand"</c>, which builds into a node belonging to the state plugin — an
    /// assembly this one does not reference. What it can ask for is
    /// <see cref="IStateLocation"/>, the contract both reach through the abstraction, and
    /// checking the field's schema here turns "that is not a record" into a build error.
    /// </remarks>
    internal static class TargetRecord
    {
        public static StatePath Resolve(INodeBuildContext context)
        {
            ExpressionNode target = context.RequireExpression("target");
            if (target is not IStateLocation location)
            {
                throw context.Error("target", "must denote a state field, such as \"$hand\".");
            }

            if (location.Path.Schema is not RecordSchemaNode)
            {
                throw context.Error("target", $"'{location.Path}' is not a record.");
            }

            return location.Path;
        }
    }
}
