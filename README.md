# Rulealize.Plugin.Record

Records in the state, for [Rulealize](https://github.com/reny-develop/Rulealize) rule sets.

| | |
| --- | --- |
| Plugin id | `Rulealize.Plugin.Record` |
| Namespace | `rec` |
| Reserved prefix | none |
| Depends on | `Rulealize.Abstraction` |
| Specification | [doc/specification.md](doc/specification.md) |

`rec.of` and `rec.map` are schema nodes; `rec.at`, `rec.has`, `rec.with` and `rec.keys` are
expressions; `rec.set` and `rec.update` are effects.

## Why

`state.schema` is a flat map of field names to schemas, so anything shaped like a map has to
be spread across one field per key. A shogi hand — seven kinds of piece for each of two
players — costs fourteen fields, fourteen reads chosen by a two-level `branch.match`, and
twenty-eight effects, because a `state.set` path is a literal and there is no way to say
"the counter for this kind".

```jsonc
"hand": {
  "op": "rec.map", "keys": ["black", "white"],
  "value": { "op": "rec.map", "keys": ["P", "L", "N", "S", "G", "B", "R"],
             "value": { "op": "type.int", "min": 0 } }
}
```

That the path stays a literal is not what is being worked around. Literal paths are what
make every path checkable before anything runs, what lets a document say which fields an
input writes, and what keeps schema validation out of evaluation. This plugin adds the other
half of a seam a grid already uses: **the field is named literally and the inside is reached
by the vocabulary of whoever owns it.** There is no `"hand.black.P"`, exactly as there is no
`"board.d3"`.

Lists did not need a plugin for the same reason a record did. A list field holds a
`Sequence`, a kind in the value model with `seq.count`, `seq.any` and `seq.where` already
pointed at it, so `type.list` in `Rulealize.Plugin.TypeSchema` is one schema node and no
operations. A record had nothing, which is the whole of why this exists.

The one decision to know before reading the specification: the key set is **closed**, and
reading a key that is not there is an error — deliberately unlike `grid.at`, which answers
null for a square off the board. A board has squares that legitimately do not exist and
Reversi's capture rule depends on reading one; a record's keys are all declared, so asking
for another is a mistake and `rec.has` is how to ask first.

## Building

`Rulealize.Abstraction` is not on nuget.org yet, so `NuGet.config` points at a folder
feed. Produce it from the abstraction repository first:

```
dotnet pack path\to\Rulealize.Abstraction\src\Rulealize.Abstraction -c Release -o path\to\LocalNuGet
```

with `LocalNuGet` a sibling of this repository. Then `dotnet build`.

## License

Apache-2.0.
