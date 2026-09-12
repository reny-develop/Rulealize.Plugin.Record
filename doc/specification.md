# Rulealize.Plugin.Record

| | |
| --- | --- |
| Identifier | `Rulealize.Plugin.Record` |
| Namespace | `rec` |
| Version | `1.0.1` |
| Reserved prefix | none |
| Depends on | [the value model](https://github.com/reny-develop/Rulealize.Abstraction/blob/main/doc/value-model.md), and nothing else |
| Notation | [how a plugin specification is written](https://github.com/reny-develop/Rulealize.Abstraction/blob/main/doc/specification-notation.md) |

Puts a record in the state and reads and writes it by a **computed key**. What asked for it
was shogi's hand. `state.set`'s `path` is a literal, so "increment the counter for piece
kind `@kind`" is unsayable and all fourteen kinds had to be written out — 72 of
`shogi.json`'s 925 lines, about 8%, were boilerplate for that one field.

## Paths stay literal

Extending `state.set`'s `path` to dotted notation was considered and rejected. It gives up
the three things [the State plugin](https://github.com/reny-develop/Rulealize.Plugin.State/blob/main/doc/specification.md) lists — checking every path up front, reading
which field an input writes straight off the document, and not carrying schema validation
into run time.

Instead it uses **the same seam as `grid.board`**: the path names the whole field, and the
inside is touched with the vocabulary of whichever plugin defined it. `"hand.black.P"` is
not writable, for the same reason `"board.d3"` is not.

## Nodes

| Node | Kind | Form |
| --- | --- | --- |
| `rec.of` | schema | `{ "op": "rec.of", "fields": { "<key>": <schema>, … } }` |
| `rec.map` | schema | `{ "op": "rec.map", "keys": ["<key>", …], "value": <schema> }` |
| `rec.at` | expression | `{ "op": "rec.at", "record": <expression>, "key": <expression:Text> }` |
| `rec.has` | expression | `{ "op": "rec.has", "record": <expression>, "key": <expression:Text> }` |
| `rec.with` | expression | `{ "op": "rec.with", "record": <expression>, "key": <expression>, "value": <expression> }` |
| `rec.keys` | expression | `{ "op": "rec.keys", "of": <expression> }` |
| `rec.set` | effect | `{ "op": "rec.set", "target": <state field>, "key": <expression>, "value": <expression> }` |
| `rec.update` | effect | `{ "op": "rec.update", "target": <state field>, "key": <expression>, "as": "<name>", "value": <expression> }` |

`rec.set` and `rec.update` take their `target` as a state field — written `"$hand"`, and
resolved when the rule set is compiled — whose schema has to be a `rec.of` or a `rec.map`.
Both halves are build errors:

```
  /inputs/spend/effects[0]/target: must denote a state field, such as "$hand".
  /inputs/spend/effects[0]/target: 'tally' is not a record.
```

The same seam `grid.set` and `graph.set` reach a field through, and the same one a third-party
vocabulary uses — see [reaching a writable state field](https://github.com/reny-develop/Rulealize.Abstraction/blob/main/README.md#reaching-a-writable-state-field).

## The key set is closed

`rec.of` and `rec.map` both declare their keys. Not an open map, on the same judgement that
put `type.enum` ahead of `type.string` plus a regular expression: **what is declared can be
checked**.

`rec.map` is separate because **it says something different**. "Every value has the same
type" cannot be expressed with `rec.of`, and shogi's hand is exactly that.

```jsonc
// shogi's hand. Fourteen fields, once
"hand": {
  "op": "rec.map", "keys": ["black", "white"],
  "value": { "op": "rec.map", "keys": ["P", "L", "N", "S", "G", "B", "R"],
             "value": { "op": "type.int", "min": 0 } }
}
```

## Reading an absent key is a fault

**The leniency of `grid.at` is not inherited.** A board legitimately has squares that do
not exist — off the board — and Reversi's rules depend on reading one and getting `null`.
Every key of a record is declared, so asking for another one is a mistake, and no rule
depends on it being quiet.

The way to ask first is `rec.has`. Shogi, adding a captured piece to a hand, has to check
that the piece is a kind that can be held; without `rec.has` the rule set carries a list of
the seven kinds separately from the schema, and the two eventually disagree.

`rec.at` on a `Null` record gives `Null` — that is a different question, namely that there
is no record to look in. It matches `tuple.at`.

## A record cannot gain a key

`rec.with`, `rec.set` and `rec.update` all refuse a key the record does not have.
Therefore **a record that started out satisfying its schema still satisfies it however the
rule set rewrites it**, and nothing downstream has to check.

## Effects read from the draft

The same reason `grid.set` does. Two effects writing different keys of one record pile up,
and the second does not erase the first. The expressions inside an effect still read the
state as the input found it; snapshot semantics is unchanged.

```jsonc
{ "op": "rec.update", "target": "$hand", "key": "#me", "as": "h",
  "value": { "op": "rec.with", "record": "@h", "key": "@piece",
             "value": { "op": "math.sub",
                        "left": { "op": "rec.at", "record": "@h", "key": "@piece" }, "right": 1 } } }
```

## `rec.keys` is in ordinal order

Not declaration order. A record is a value, and it may have been built from a literal that
never saw a schema, so **declaration order does not always exist**. Sort order always does.
It is also what keeps `GetValidInputs` from reordering its output between runs.

## Sequences are different

A sequence field holds a `Sequence`, which is a kind of the value model, and `seq.count`,
`seq.any` and `seq.where` already read one. That is why [`type.list`](https://github.com/reny-develop/Rulealize.Plugin.TypeSchema/blob/main/doc/specification.md) is a
single schema node with no operations at all. **A record had nothing**, and that is the
entire reason this plugin exists.

---

## Decided

- **The key set stays closed.** The case for opening it is copying external data, where the
  keys may not be known in advance. That case then arrived — roster assigns real people to
  real shifts — and did not want an open record: the people belong in a
  [`type.list`](https://github.com/reny-develop/Rulealize.Plugin.TypeSchema/blob/main/doc/specification.md) of `rec.of`, because **a key set fixed by the instance is not
  a key set at all, it is a list**. It is the same distinction that decides between `rec.of`
  and `rec.map`: `rec.map`'s keys are right when the domain fixes them, as shogi's seven
  piece kinds are fixed by the rules of shogi.
- **A record cannot be an input argument, and that is correct.** It has no canonical text,
  so a domain returning records fails when an argument is resolved. A compound input uses
  [Tuple](https://github.com/reny-develop/Rulealize.Plugin.Tuple/blob/main/doc/specification.md), which exists for exactly that.
- **Invariants across fields still cannot be written.** "The pawns on the board and in both
  hands total eighteen" has no home: `rec.of` and `rec.map` constrain one field, and there
  is no predicate language for `state.schema`. Since a transition now checks what its
  effects built ([TypeSchema](https://github.com/reny-develop/Rulealize.Plugin.TypeSchema/blob/main/doc/specification.md)), there is at last a *place* such a check would
  run — which is the part that used to be missing — but the language to write one in is
  not there, and adding it means a new reserved key in a document whose reserved keys are
  deliberately eight. Both shogi and roster record wanting it, neither is blocked by not
  having it, and it stays unbuilt until one is.
- **`rec.of` is where inference would start.** A record of heterogeneous fields is one of
  the few places where the type of what `rec.at` returns is statically determined, so it is
  the natural foothold. It waits on inference generally
  ([TypeSchema](https://github.com/reny-develop/Rulealize.Plugin.TypeSchema/blob/main/doc/specification.md) records the condition).
