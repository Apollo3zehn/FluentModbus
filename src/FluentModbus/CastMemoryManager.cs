using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace FluentModbus
{
    internal class CastMemoryManager<TFrom, TTo> : MemoryManager<TTo>
            where TFrom : struct
            where TTo : struct
    {
        private readonly Memory<TFrom> _from;

        public CastMemoryManager(Memory<TFrom> from) => _from = from;

        public override Span<TTo> GetSpan() => MemoryMarshal.Cast<TFrom, TTo>(_from.Span);

        protected override void Dispose(bool disposing)
        {
            //
        }

        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

        public override void Unpin() => throw new NotSupportedException();
    }
}
