# P4 Debuff and Movement Guide

Source raidplans:

- https://raidplan.io/plan/j33r35wvfp5xg7dd
- https://raidplan.io/plan/guufe9q559evt7pj

This guide describes what each debuff means and how players should move or act
when it resolves. The plan assumes the shown party layout:

- Supports: `PLD`, `DRK`, `AST`, `SGE`
- DPS: `RPR`, `SAM`, `MCH`, `RDM`
- Short spread chains: `SGE` west/support side, `MCH` east/DPS side
- Long spread ignores: `AST` west/support side, `RDM` east/DPS side

## Personal Information To Remember

Every player must track two personal assignments:

- Stack/spread timing: short or long.
- Bomb behavior: Stillness or Motion.

The raid also needs shared callouts for:

- Exdeath shrieks: `Gaze 1` and `Gaze 2`, real or fake.
- Chaos planted AoEs: Inferno and Tsunami, real or fake.
- Kefka stored Thunder/Ice and Mana Release, real or fake.

## Neo Exdeath Stack/Spread Debuffs

These debuffs decide whether a player stacks with their group or spreads away.
Each set gives one support and one DPS each debuff type.

### Stack Debuff

Meaning:

- If the cast is real, this player is a 3-person stack target.
- If the cast is fake, this player spreads.

Movement:

- Real short: resolve on the first stack/spread resolution.
- Real long: resolve on the later stack/spread resolution.
- Fake short: spread on the first stack/spread resolution.
- Fake long: spread on the later stack/spread resolution.

Group handling:

- Supports resolve north or west depending on the set.
- DPS resolve south or east depending on the set.
- The stack player joins their role group with two other players.
- The spread player leaves the group and uses the assigned outside position.

Important:

- The second Neo Exdeath stack/spread set is opposite of the first one.
- If you stacked first, expect to spread later; if you spread first, expect to
  stack later.

### Spread Debuff

Meaning:

- If the cast is real, this player spreads.
- If the cast is fake, this player is a 3-person stack target.

Movement:

- Real short: spread on the first stack/spread resolution.
- Real long: spread on the later stack/spread resolution.
- Fake short: resolve as a 3-person stack on the first stack/spread resolution.
- Fake long: resolve as a 3-person stack on the later stack/spread resolution.

Group handling:

- Supports use the support side.
- DPS use the DPS side.
- Spreads go out and away from the role stack.
- Stacks stay grouped as three players.

Important:

- The plan treats stack/spread identity and timer as the personal memory check.
- You should know both your action and whether it is short or long before the
  first resolution begins.

## Acceleration Bomb: Stillness and Motion

This debuff checks whether you are allowed to act or move when it expires.

### Stillness

Meaning:

- Stop all actions when the debuff expires.
- This includes weaponskills, spells, abilities, auto-attacks if possible, and
  movement inputs at the final moment.

Movement and action:

- Pre-position before the timer expires.
- Stop pressing buttons before the debuff resolves.
- Do not slidecast or weave through the expiration.
- Resume actions only after the debuff has resolved.

Common failure:

- Continuing a cast, pressing an oGCD, or moving at expiration.

### Motion

Meaning:

- Keep moving when the debuff expires.

Movement and action:

- Start moving before the expiration.
- Keep movement active through the resolve.
- Do not stand still at the final moment.
- You can use instant actions if they do not stop your movement.

Common failure:

- Reaching a safe spot early and stopping before the timer expires.

Important:

- The timer can be short or long.
- Each player only receives one bomb assignment total in this sequence.
- The plan marks Stillness as the real bomb and Motion as the fake bomb.

## Cursed Shriek / Gaze

Shriek creates gaze checks around marked players. The raidplan calls these
`Gaze 1` and `Gaze 2`.

### Real Shriek

Meaning:

- Look away from the shriek players.

Movement:

- Move to the assigned line or stack position first.
- Face away from the marked shriek players before the gaze resolves.
- Keep your character facing out until the resolve is complete.

Shotcall:

- The caller checks Exdeath during the cast and calls `Gaze 1 real` or
  `Gaze 2 real`.

Common failure:

- Waiting for the visual resolve instead of reacting to the real/fake call.

### Fake Shriek

Meaning:

- Look toward the shriek players.
- The second raidplan phrases this as `Look INSIDE`.

Movement:

- Move to the assigned line or stack position first.
- Face inward/toward the shriek players before the gaze resolves.
- Hold that facing until the resolve is complete.

Shotcall:

- The caller checks Exdeath during the cast and calls `Gaze 1 fake` or
  `Gaze 2 fake`.

Common failure:

- Treating fake shriek like a normal look-away gaze.

Important:

- Short shrieks resolve earlier.
- Long shrieks resolve later.
- The group should not wait for the cast to finish before calling real or fake.

## Chaos Planted AoEs

Chaos gives planted delayed AoEs. Players drop them first, then dodge the result
about 5 seconds later.

### Inferno / Fire

Inferno always resolves before Tsunami.

Real Inferno:

- Effect: circle/PBAoE.
- Movement: drop the puddle, then get out of the circle.
- Safe area: away from the planted AoE.

Fake Inferno:

- Effect: donut AoE.
- Movement: drop the puddle, then stay in or move into the donut safe zone.
- Safe area: near the planted AoE, not outside the ring.

Group handling:

- Puddles are dropped in the middle in the shown plan.
- After planting, move to the next role-side assignment.
- Do not overreact to Ultima Upsurge; wait for the delayed AoE if needed.

### Tsunami / Water

Tsunami always resolves after Inferno.

Real Tsunami:

- Effect: donut AoE.
- Movement: drop the puddle, then stay in or move into the donut safe zone.
- Safe area: near the planted AoE.

Fake Tsunami:

- Effect: circle/PBAoE.
- Movement: drop the puddle, then get out of the circle.
- Safe area: away from the planted AoE.

Group handling:

- Water puddles are also dropped in the middle in the shown plan.
- Mana Release happens while water puddles are being planted.
- Do not lose the stored Thunder/Ice call while resolving water.

Important:

- Fire resolves first.
- Water resolves second.
- The shotcaller should call what Chaos is casting as soon as it is visible.

## Flood of Naught / Black and White Antilight

This mechanic gives players color-related debuffs. The plan says to ignore
fake/real on the initial cast for movement; it mainly changes healing required.

### Purple / BD

Meaning:

- You want your own color.

Movement:

- Identify your wound/color.
- Go to the same matching color.
- If Exdeath has `??`, flip to the opposite side.

Memory shortcut:

- Say internally: `I want purple` or `I want blue`.

### Yellow / AF

Meaning:

- You want the opposite color.

Movement:

- Identify your wound/color.
- Go to the opposite color.
- If Exdeath has `??`, flip to the other side after solving.

Memory shortcut:

- Solve the final destination, not the raw debuff name.
- Say internally: `I want purple` or `I want blue`.

Important:

- Go to the solved color quickly.
- Check Exdeath for `??`.
- If `??` is present, flip after solving.

## Thunder III

Thunder is one of Kefka's stored mechanics.

Meaning:

- Kefka casts a lightning pattern during the middle of the sequence.
- The group must dodge the immediate lightning and remember whether Thunder was
  real or fake.

Movement:

- Align with the Thunder AoE.
- Dodge according to the real/fake tell.
- After dodging, call or record whether Thunder was real.

Later use:

- Kefka stores this mechanic.
- Mana Release later determines whether the stored Thunder stays the same or
  flips.

## Blizzard III / Ice

Ice is the other Kefka stored mechanic.

Meaning:

- Kefka casts an ice pattern later in the sequence.
- The group must dodge the immediate ice and remember whether Ice was real or
  fake.

Movement:

- Dodge the ice quarter cleaves or safe quadrant based on the real/fake tell.
- After dodging, call or record whether Ice was real.

Later use:

- Kefka stores this mechanic.
- Mana Release later determines whether the stored Ice stays the same or flips.

## Mana Release

Mana Release decides what the stored Thunder and Ice become at the final dodge.

Meaning:

- True release keeps the stored mechanic.
- Fake release flips the stored mechanic.

Examples:

- True Ice plus true release becomes true Ice.
- True Ice plus fake release becomes fake Ice.
- Fake Ice plus true release becomes fake Ice.
- Fake Ice plus fake release becomes true Ice.

Movement:

- While water puddles are being planted, watch or listen for the release calls.
- Resolve the delayed water AoE.
- Then dodge the final combined stored Thunder/Ice result.

Important:

- Kefka can fake Mana Release.
- Do not assume the stored mechanic stays unchanged.
- The final dodge can require staying out of the middle, roughly outside the
  boss hitbox, if the stored result is a center AoE.

## Role And Player Movement Summary

### Supports

Players:

- `PLD`
- `DRK`
- `AST`
- `SGE`

Default movement:

- Use north for north/south splits.
- Use west for west/east split mechanics.
- Stay with the support stack unless assigned to spread, chain, or ignore.

### DPS

Players:

- `RPR`
- `SAM`
- `MCH`
- `RDM`

Default movement:

- Use south for north/south splits.
- Use east for west/east split mechanics.
- Stay with the DPS stack unless assigned to spread, chain, or ignore.

### Short Spread Chains

Players:

- `SGE`: support chain, west.
- `MCH`: DPS chain, east.

Movement:

- Go out during the first chain/spread set.
- Resolve your short spread away from the role stack.
- Watch for Motion/Stillness if your bomb resolves during this set.

### Long Spread Ignores

Players:

- `AST`: support ignore, west.
- `RDM`: DPS ignore, east.

Movement:

- Go out during the later ignore/spread set.
- Resolve your long spread away from the role stack.
- Watch for Motion/Stillness if your bomb resolves during this set.

## Common Wipe Causes

- Forgetting whether your stack/spread is short or long.
- Forgetting that the second stack/spread set is opposite of the first.
- Pressing an action during Stillness.
- Stopping movement during Motion.
- Looking away on fake shriek or looking in on real shriek.
- Dropping Inferno or Tsunami correctly but dodging the delayed AoE backward.
- Solving Antilight color but forgetting to flip when Exdeath has `??`.
- Remembering Thunder or Ice incorrectly before Mana Release.
- Forgetting that Mana Release itself can be fake.
