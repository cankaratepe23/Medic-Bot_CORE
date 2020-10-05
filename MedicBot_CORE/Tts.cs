using System;

namespace MedicBot
{
    internal class Tts
    {
        internal static Uri GetTtsLink(string text)
        {
            return new Uri($"https://translate.google.com/translate_tts?ie=UTF-8&tl=tr-TR&client=tw-ob&q={Uri.EscapeDataString(text)}");
        }
    }
}