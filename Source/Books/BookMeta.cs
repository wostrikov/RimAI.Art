using System;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.Books
{
    /// <summary>
    /// 书籍元信息（只读取“确定存在”的字段）。
    /// - Vanilla：CompBook + CompProperties_Book 可稳定读取（nameMaker/descriptionMaker/pickWeight/ageYearsRange/questChance）。
    /// - VBE：Newspaper 的 expireTime/expireTimeAbs 等仅在 VBE 类型存在时通过反射读取（不要求编译期依赖）。
    /// </summary>
    public sealed class BookMeta
    {
        public Thing Thing { get; }
        public Book Book { get; }                 // Verse.Book（如果 thing 不是 Book，则为 null）
        public CompBook CompBook { get; }         // RimWorld.CompBook（如果无 comp，则为 null）
        public CompProperties_Book BookProps { get; } // 由 CompBook.Props 提供（如果 CompBook 为空则为 null）

        public BookType Type { get; }

        public string DefName { get; }
        public string ModName { get; }
        public string PackageId { get; }

        // Vanilla/通用可读信息
        public string Title { get; }
        public string FlavorUI { get; }
        public string DescriptionDetailed { get; }

        // Vanilla（来自 Verse.Book 的公开属性）
        public float MentalBreakChancePerHour { get; }
        public float JoyFactor { get; }

        // Vanilla（来自 CompProperties_Book，若存在）
        public string NameMakerDefName { get; }
        public string DescriptionMakerDefName { get; }
        public float PickWeight { get; }
        public FloatRange AgeYearsRange { get; }
        public float QuestChance { get; }

        // VBE：RecipeSkillBook（DefModExtension）信息（若存在）
        public string SkillDefName { get; }

        // VBE：Newspaper 特有字段（若不是 Newspaper，则为 null）
        public int? VbeExpireTime { get; }
        public int? VbeExpireTimeAbs { get; }

        public BookMeta(
            Thing thing,
            BookType type,
            string skillDefName = null,
            int? vbeExpireTime = null,
            int? vbeExpireTimeAbs = null)
        {
            Thing = thing ?? throw new ArgumentNullException(nameof(thing));
            Type = type;

            Book = thing as Book;
            CompBook = thing.TryGetComp<CompBook>();
            BookProps = CompBook?.Props;

            var def = thing.def;
            DefName = def?.defName ?? "UnknownDef";
            ModName = def?.modContentPack?.Name ?? "Unknown";
            PackageId = def?.modContentPack?.PackageId ?? "Unknown";

            // Title/FlavorUI/DescriptionDetailed 只在 Verse.Book 上可稳定获取
            if (Book != null)
            {
                Title = Book.Title ?? thing.LabelCap;
                FlavorUI = Book.FlavorUI ?? string.Empty;
                DescriptionDetailed = Book.DescriptionDetailed ?? string.Empty;
                MentalBreakChancePerHour = Book.MentalBreakChancePerHour;
                JoyFactor = Book.JoyFactor;
            }
            else
            {
                Title = thing.LabelCap;
                FlavorUI = string.Empty;
                DescriptionDetailed = thing.DescriptionDetailed ?? string.Empty;
                MentalBreakChancePerHour = 0f;
                JoyFactor = 1f;
            }

            // CompProperties_Book（Vanilla 反编译保证字段存在）
            if (BookProps != null)
            {
                NameMakerDefName = BookProps.nameMaker?.defName ?? string.Empty;
                DescriptionMakerDefName = BookProps.descriptionMaker?.defName ?? string.Empty;
                PickWeight = BookProps.pickWeight;
                AgeYearsRange = BookProps.ageYearsRange;
                QuestChance = BookProps.questChance;
            }
            else
            {
                NameMakerDefName = string.Empty;
                DescriptionMakerDefName = string.Empty;
                PickWeight = 1f;
                AgeYearsRange = new FloatRange(0f, 0f);
                QuestChance = 0f;
            }

            SkillDefName = skillDefName ?? string.Empty;
            VbeExpireTime = vbeExpireTime;
            VbeExpireTimeAbs = vbeExpireTimeAbs;
        }

        public override string ToString()
        {
            return $"{Title} ({DefName}) [{Type}]";
        }
    }
}
