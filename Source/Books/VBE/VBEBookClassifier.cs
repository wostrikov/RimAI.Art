using System;
using System.Reflection;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.Books.VBE
{
    public sealed class VBEBookClassifier : IBookClassifier
    {
        // 来自你提供的 VBE 源码：namespace VanillaBooksExpanded, class Newspaper : Book
        private const string VbeNewspaperTypeName = "VanillaBooksExpanded.Newspaper";

        // 来自你提供的 VBE 源码：namespace VanillaBooksExpanded, class RecipeSkillBook : DefModExtension { SkillDef skill; }
        private const string VbeRecipeSkillBookExtensionTypeName = "VanillaBooksExpanded.RecipeSkillBook";
        private const string VbeRecipeSkillBookSkillFieldName = "skill";

        public BookMeta TryClassify(Thing thing)
        {
            if (thing == null) return null;

            // 1) Newspaper：运行时类型名精确匹配（不需要编译期引用 VBE 程序集）
            var typeName = thing.GetType().FullName;
            if (string.Equals(typeName, VbeNewspaperTypeName, StringComparison.Ordinal))
            {
                // 读取 expireTime / expireTimeAbs（字段在 VBE Newspaper 类中定义）
                // public int expireTime; public int expireTimeAbs; 见源码文件
                int? expireTime = TryGetIntField(thing, "expireTime");
                int? expireTimeAbs = TryGetIntField(thing, "expireTimeAbs");

                return new BookMeta(
                    thing,
                    BookType.VBE_Newspaper,
                    skillDefName: null,
                    vbeExpireTime: expireTime,
                    vbeExpireTimeAbs: expireTimeAbs);
            }

            // 2) SkillBook：通过 DefModExtension 探测 RecipeSkillBook 扩展
            // 不猜 defName/分类，仅检查 modExtensions 中是否存在该类型名
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
            // ThingDef.modExtensions 是 List<DefModExtension>（Verse 运行时）
            // 这里不硬引用 VanillaBooksExpanded.RecipeSkillBook 类型；只按 FullName 匹配
            var exts = def.modExtensions;
            if (exts == null || exts.Count == 0) return null;

            for (int i = 0; i < exts.Count; i++)
            {
                var ext = exts[i];
                if (ext == null) continue;

                var extType = ext.GetType();
                if (!string.Equals(extType.FullName, VbeRecipeSkillBookExtensionTypeName, StringComparison.Ordinal))
                    continue;

                // 读取 public SkillDef skill;
                var field = extType.GetField(VbeRecipeSkillBookSkillFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null) return "UnknownSkill";

                var val = field.GetValue(ext);
                if (val is SkillDef skillDef)
                    return skillDef.defName;

                // 如果类型不匹配但字段存在，仍返回一个标记，避免 silent fail
                return "UnknownSkill";
            }

            return null;
        }
    }
}
