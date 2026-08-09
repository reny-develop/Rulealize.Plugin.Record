# Rulealize.Plugin.Record

Records in the state, for [Rulealize](https://github.com/reny-develop/Rulealize) rule sets.

| | |
| --- | --- |
| Plugin id | `Rulealize.Plugin.Record` |
| Namespace | `rec` |
| Reserved prefix | none |
| Depends on | `Rulealize.Abstraction` |

## Why

`state.schema` is a flat map of field names to schemas, so anything shaped like a map has to
be spread across one field per key. A shogi hand — seven kinds of piece for each of two
players — costs fourteen fields:

```jsonc
"bP": { "op": "type.int", "min": 0 },
"bL": { "op": "type.int", "min": 0 },
// … twelve more
```

fourteen reads chosen by a two-level `branch.match`, and twenty-eight effects, because a
`state.set` path is a literal and there is no way to say "the counter for this kind".

```jsonc
"hand": {
  "op": "rec.map", "keys": ["black", "white"],
  "value": { "op": "rec.map", "keys": ["P", "L", "N", "S", "G", "B", "R"],
             "value": { "op": "type.int", "min": 0 } }
}
```

## The path stays a literal

That restriction is not what is being worked around. Literal paths are what make every path
checkable before anything runs, what lets a document say which fields an input writes, and
what keeps schema validation out of evaluation.

This plugin adds the other half of a seam a grid already uses: **the field is named
literally and the inside is reached by the vocabulary of whoever owns it.** There is no
`"hand.black.P"`, exactly as there is no `"board.d3"`.

## Operations

| Operation | Kind | Shape |
| --- | --- | --- |
| `rec.of` | schema | `{ "op": "rec.of", "fields": { "<key>": <schema>, … } }` |
| `rec.map` | schema | `{ "op": "rec.map", "keys": ["<key>", …], "value": <schema> }` |
| `rec.at` | expression | `{ "op": "rec.at", "record": …, "key": … }` |
| `rec.has` | expression | `{ "op": "rec.has", "record": …, "key": … }` |
| `rec.with` | expression | `{ "op": "rec.with", "record": …, "key": …, "value": … }` |
| `rec.keys` | expression | `{ "op": "rec.keys", "of": … }` |
| `rec.set` | effect | `{ "op": "rec.set", "target": "$<field>", "key": …, "value": … }` |
| `rec.update` | effect | `{ "op": "rec.update", "target": "$<field>", "key": …, "as": "<name>", "value": … }` |

`rec.map` exists beside `rec.of` because it says something the other form cannot — that
every value has the same type. Both declare a **closed** key set, which is what lets a
mistyped key be an error rather than a silent null, and what gives `rec.keys` something to
answer. It is the same judgement that put `type.enum` ahead of a string with a pattern.

## Reading a key that is not there is an error

Deliberately unlike `grid.at`, which answers null for a square off the board.

A board has squares that legitimately do not exist, and Othello's capture rule depends on
reading one and getting null. A record's keys are all declared; asking for another one is a
mistake, and no rule anywhere is relying on it being quiet.

`rec.has` is how to ask first. Shogi puts a captured piece into a hand and has to find out
whether the thing it took is a kind a hand holds — without `rec.has` the rule set would keep
its own list of the seven kinds beside the schema, for the two to drift apart later.

A null record still reads as null, which is a different question: there is no record to ask.

## A record cannot grow a key

`rec.with` refuses a key the record does not already have, and `rec.set` and `rec.update`
refuse it too. So a record that started inside its schema stays inside it however a rule set
rewrites it, and nothing downstream has to check.

## Effects read the draft

`rec.set` and `rec.update` take the current record from the draft rather than the snapshot,
the way `grid.set` takes its board. Two effects writing different keys of one record add up
instead of the second undoing the first; the expressions inside them still read the state as
it was when the input arrived.

`rec.update` binds what the key already holds, which is what counting wants:

```jsonc
{ "op": "rec.update", "target": "$hand", "key": "#me", "as": "h",
  "value": { "op": "rec.with", "record": "@h", "key": "@piece",
             "value": { "op": "math.sub",
                        "left": { "op": "rec.at", "record": "@h", "key": "@piece" }, "right": 1 } } }
```

## Lists did not need a plugin

A list field holds a `Sequence`, which is a kind in the value model with a plugin's worth of
vocabulary already pointed at it — `seq.count`, `seq.any`, `seq.where`. `type.list` in
`Rulealize.Plugin.TypeSchema` is one schema node and no operations. A record had nothing,
which is the whole of why this exists.

## `rec.keys` is in ordinal order

Not declaration order. A record is a value and may have been built by a literal that no
schema ever saw, so declaration order is not always a thing that exists; sorting is the only
order that always does, and results should not depend on which run they were.

## Building

`Rulealize.Abstraction` is not on nuget.org yet, so `NuGet.config` points at a folder
feed. Produce it from the abstraction repository first:

```
dotnet pack path\to\Rulealize.Abstraction\src\Rulealize.Abstraction -c Release -o path\to\LocalNuGet
```

with `LocalNuGet` a sibling of this repository. Then `dotnet build`.

## License

Apache-2.0.
