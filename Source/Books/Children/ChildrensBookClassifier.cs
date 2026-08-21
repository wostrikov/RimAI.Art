/*
 * File: ChildrensBookClassifier.cs
 *
 * Purpose:
 * - Identify books added by the Children's Books mod.
 *
 * Dependencies:
 * - Verse.Book
 * - Childrens_Books.ChildrensBookDefOf (via reflection)
 * - DefDatabase<ThingDef> (fallback only)
 *
 * Identification rules (authoritative):
 * - Thing MUST be Verse.Book
 * - Thing.def MUST equal:
 *   - ChildrensBookDefOf.BBLK_ChildrensBook, OR
 *   - ChildrensBookDefOf.BBLK_ColoringBook
 *
 * Implementation notes:
 * - Prefer reflection to read static fields from ChildrensBookDefOf.
 * - Fallback to DefDatabase.GetNamedSilentFail ONLY if reflection fails.
 * - Do NOT hard-reference the Children's Books assembly.
 *
 * Do NOT:
 * - Do not infer type from Doers or description text.
 * - Do not classify generic CompBook items here.
 */


using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Verse;

namespace Ustas.RimAI.Art.Books.Children
{
    public sealed class ChildrensBookClassifier : Ustas.RimAI.Art.Books.IBookClassifier
    {
        private const string DefOfTypeFullName = "Childrens_Books.ChildrensBookDefOf";
        private const string FieldChildrensBook = "BBLK_ChildrensBook";
        private const string FieldColoringBook = "BBLK_ColoringBook";

        private static bool _resolved;
        private static ThingDef _childrensBookDef;
        private static ThingDef _coloringBookDef;

        public BookMeta TryClassify(Thing thing)
        {
            if (thing == null) return null;

            EnsureResolved();

            if (!(thing is Book)) return null;

            var def = thing.def;
            if (def == null) return null;

            if (_childrensBookDef != null && def == _childrensBookDef)
                return new BookMeta(thing, BookType.CB_ChildrensBook);

            if (_coloringBookDef != null && def == _coloringBookDef)
                return new BookMeta(thing, BookType.CB_ColoringBook);

            return null;
        }

        private static void EnsureResolved()
        {
            if (_resolved) return;
            _resolved = true;

            try
            {
                var defOfType = FindTypeInLoadedAssemblies(DefOfTypeFullName);
                if (defOfType != null)
                {
                    _childrensBookDef = ReadStaticThingDef(defOfType, FieldChildrensBook);
                    _coloringBookDef = ReadStaticThingDef(defOfType, FieldColoringBook);
                }
            }
            catch (TypeLoadException)
            {
            }
            catch (ReflectionTypeLoadException)
            {
            }
            catch (FileLoadException)
            {
            }
            catch (TargetException)
            {
            }
            catch (ArgumentException)
            {
            }

            if (_childrensBookDef == null)
                _childrensBookDef = DefDatabase<ThingDef>.GetNamedSilentFail(FieldChildrensBook);

            if (_coloringBookDef == null)
                _coloringBookDef = DefDatabase<ThingDef>.GetNamedSilentFail(FieldColoringBook);
        }

        private static ThingDef ReadStaticThingDef(Type defOfType, string fieldName)
        {
            var field = defOfType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            if (field == null) return null;
            if (!typeof(ThingDef).IsAssignableFrom(field.FieldType)) return null;
            return field.GetValue(null) as ThingDef;
        }

        private static Type FindTypeInLoadedAssemblies(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                var asm = assemblies[i];
                Type t = null;
                try
                {
                    t = asm.GetType(fullName, throwOnError: false);
                }
                catch (TypeLoadException)
                {
                }
                catch (FileLoadException)
                {
                }
                catch (BadImageFormatException)
                {
                }
                catch (ArgumentException)
                {
                }

                if (t != null) return t;
            }

            return null;
        }
    }
}
