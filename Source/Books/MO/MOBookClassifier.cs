using System;
using System.Collections.Generic;
using Verse;

namespace Ustas.RimAI.Art.Books.MO
{
    public sealed class MOBookClassifier : IBookClassifier
    {
        private const string MoBookWithAuthorTypeName = "MedievalOverhaul.BookWithAuthor";
        private const string MoDefinableBookCompPropsTypeName = "MedievalOverhaul.CompProperties_DefinableBook";

        public BookMeta TryClassify(Thing thing)
        {
            if (thing == null) return null;

            var typeName = thing.GetType().FullName;
            if (string.Equals(typeName, MoBookWithAuthorTypeName, StringComparison.Ordinal))
                return new BookMeta(thing, BookType.MO_DefinableBook);

            var def = thing.def;
            if (def?.comps != null && HasCompProps(def.comps))
                return new BookMeta(thing, BookType.MO_DefinableBook);

            return null;
        }

        private static bool HasCompProps(List<CompProperties> comps)
        {
            for (int i = 0; i < comps.Count; i++)
            {
                var comp = comps[i];
                if (comp == null) continue;
                var compType = comp.GetType();
                if (string.Equals(compType.FullName, MoDefinableBookCompPropsTypeName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
