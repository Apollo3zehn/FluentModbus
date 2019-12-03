using System;

namespace FluentModbus
{
    [AttributeUsage(AttributeTargets.All, Inherited = false)]
    sealed class HideFromApiAttribute : Attribute
    {
        public HideFromApiAttribute()
        {
            //
        }
    }
}