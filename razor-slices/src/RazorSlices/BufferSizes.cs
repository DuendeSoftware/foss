// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.RazorSlices;

internal static class BufferSizes
{
    public const int MaxUtf8BytesPerScalar = 4;
    public const int SmallNumericWriteByteSize = 32;
    public const int SmallNumericWriteCharSize = SmallNumericWriteByteSize / 2;
    public const int SmallFormattableWriteByteSize = 64;
    public const int SmallFormattableWriteCharSize = SmallFormattableWriteByteSize / 2;
    public const int SmallTextWriteByteSize = 256;
    public const int SmallTextWriteCharSize = SmallTextWriteByteSize / 2;
    public const int MaxBufferSize = 1024;
    public const double HtmlEncodeAllowanceRatio = 1.1; // Generally allow 10% of input size

    public static int GetHtmlEncodedSizeHint(int length) => Math.Min(MaxBufferSize, (int)Math.Round(length * HtmlEncodeAllowanceRatio));
}
