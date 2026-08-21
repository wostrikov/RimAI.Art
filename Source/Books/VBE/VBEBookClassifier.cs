using System;
using System.Reflection;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.Books.VBE
{
    public sealed class VBEBookClassifier : IBookClassifier
    {
        private const string VbeNewspaperTypeName = "VanillaBooksExpanded.Newspaper";

        private const string VbeRecipeSkillBookExtensionTypeName = "VanillaBooksExpanded.RecipeSkillBook";
        private const string VbeRecipeSkillBookSkillFieldName = "skill";

        public BookMeta TryClassify(Thing thing)
        {
            if (thing == null) return null;

            var typeName = thing.GetType().FullName;
            if (string.Equals(typeName, VbeNewspaperTypeName, StringComparison.Ordinal))
            {
                int? expireTime = TryGetIntField(thing, "expireTime");
                int? expireTimeAbs = TryGetIntField(thing, "expireTimeAbs");

                return new BookMeta(
                    thing,
                    BookType.VBE_Newspaper,
                    skillDefName: null,
                    vbeExpireTime: expireTime,
                    vbeExpireTimeAbs: expireTimeAbs);
            }

            if (thing.def != null)
            {
                var skillDefName = TryGetSkillDefNameFromRecipeSkillBookExtension(thing.def);
                if (!string.IsNullOrEmpty(skillDefName))
                {
                    return new BookMeta(
                        thing,
                        BookType.VBE_SkillBook,
                        skillDefName: skillDefName);
                }
            }

            return null;
        }

        private static int? TryGetIntField(object obj, string fieldName)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) return null;
            if (f.FieldType != typeof(int)) return null;
            return (int)f.GetValue(obj);
        }

        private static string TryGetSkillDefNameFromRecipeSkillBookExtension(ThingDef def)
        {
            var exts = def.modExtensions;
            if (exts == null || exts.Count == 0) return null;

            for (int i = 0; i < exts.Count; i++)
            {
                var ext = exts[i];
                if (ext == null) continue;

                var extType = ext.GetType();
                if (!string.Equals(extType.FullName, VbeRecipeSkillBookExtensionTypeName, StringComparison.Ordinal))
                    continue;

                var field = extType.GetField(VbeRecipeSkillBookSkillFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null) return "UnknownSkill";

                var val = field.GetValue(ext);
                if (val is SkillDef skillDef)
                    return skillDef.defName;

                return "UnknownSkill";
            }

            return null;
        }
    }
}
