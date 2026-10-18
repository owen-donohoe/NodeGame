---
shell: concept
used-by:
  - "[[Tick packets]]"
  - "[[InputSerializer]]"
tags: [concept, topic/networking, topic/serialization]
---

# Endianness

> [!question] The question
> An `int` is four bytes. Which one goes first, and why does it matter when two machines
> exchange bytes?

**Kind:** concept. True outside Node War. Used by [[Tick packets]] and [[InputSerializer]].

## The idea

**Byte order is a convention. *Little-endian* writes the lowest byte first, *big-endian* the
highest. Any file or packet format must pick one and write it explicitly, never "whatever
this CPU does".**

```
value 300 = 0x0000012C

little-endian:  2C 01 00 00     (x86, ARM: what almost every CPU uses natively)
big-endian:     00 00 01 2C     ("network byte order" in classic internet protocols)
```

## In Node War

`InputSerializer.WriteInt` writes `value`, `value >> 8`, `value >> 16`, `value >> 24` in that
order: **little-endian, by hand**. Every machine produces the same bytes regardless of CPU,
runtime or compiler.

## Why not the alternatives

| Option | Problem |
|---|---|
| `BitConverter.GetBytes` | Uses the *machine's* order (`BitConverter.IsLittleEndian`). It's almost always little today, but that's not a guarantee you can see in the code |
| Copying raw struct memory | Adds padding and alignment, and relies on field layout. Fragile across compilers |
| `BinaryPrimitives.WriteInt32LittleEndian` | A fine modern choice: explicit and fast. The hand-written shifts do the same thing |

## Check yourself

> [!question]- What bytes does `WriteInt` produce for 1? For −1?
> 1 → `01 00 00 00`. −1 → `FF FF FF FF` (two's complement: all bits set).

## Learn more

- Wikipedia, [Endianness](https://en.wikipedia.org/wiki/Endianness)

## My understanding

<!-- Yours. AI never edits below this line. -->
