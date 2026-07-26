# Translation brief — Pass the Fear → Thai

Read this before translating a batch. It is short on purpose.

## What the game is

A co-op roguelike shooter. Cartoon art, light tone, tarot-themed relics. Players run through
chapters, pick up weapons, weapon parts, relics and pearls, and fight bosses. Not a serious or
literary game — do not translate it like one.

## Register

**Plain modern Thai, casual but not slangy.** The kind of Thai a well-localised indie game uses.

- Pronouns: `คุณ` for the player where a pronoun is unavoidable. Usually it is avoidable — Thai
  drops subjects happily, and shorter is better in a cramped UI.
- No `ครับ/ค่ะ` in UI labels, system text, item descriptions. Only in NPC dialogue if it reads
  naturally there.
- No royal or archaic register. This is not a wuxia game.

## Do NOT over-research

If a term has no obvious Thai equivalent, **pick something sensible and move on**. Do not spend
time hunting for an established fan translation, a canonical rendering, or the "correct" term.
A reasonable, consistent, readable choice beats a perfectly sourced one, and a batch that ships
beats one that is still being researched.

Transliterate proper names into Thai script by ear. `Alice` → `อลิซ`. `Ludwig` → `ลุดวิก`. Done.

## Source columns

Each row is `key <TAB> english <TAB> chinese`.

**English is the reference; Chinese is the tiebreaker.** The game was written in Chinese and the
English is itself a translation, so when the English is ambiguous, terse, or obviously awkward,
the Chinese tells you what was meant. You do not need to read Chinese to use it — the shape of it
(how many characters, whether it repeats a term used elsewhere) is often enough.

## Rules that will break the game if ignored

1. **Never change the key.** Copy it exactly, including case and dots.
2. **Output is `key <TAB> thai`.** One tab. No quotes around anything.
3. **Preserve every placeholder exactly**: `{0}`, `{0:N0}`, `{1:P0}`, `{$var}`, `<size=24>`,
   `<color=#FF0000>`, `\n`. They can move within the sentence if Thai word order needs it, but
   the token itself is copied character for character.
4. **A literal tab cannot appear in a value.** Use `\t`. Newlines are `\n`.
5. **Keep it short.** These boxes were laid out for 2–4 Chinese characters. Thai is much wider.
   Where a shorter word will do, use it — `ตั้งค่า` not `การตั้งค่า`, `ออก` not `ออกจากเกม` when
   the box is tiny. Aim to not exceed the English length by much.
6. **UTF-8, LF line endings, no BOM.**

## Consistency

Check `loc/glossary.tsv` before inventing a term, and add any term you settle on that other
batches will also hit. Weapons, relics, stats and status effects recur everywhere.

## Output

Write `loc/th/<same-name-as-source>.tsv`. One line per source row, same order. Header comment
line optional. If you genuinely cannot translate a row, still emit it with the English as the
value rather than dropping the line — a missing key silently falls back to English anyway, but
an explicit row is reviewable.
