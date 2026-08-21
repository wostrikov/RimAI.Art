using System;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.Books
{
    public sealed class BookMeta
    {
        public Thing Thing { get; }
        public Book Book { get; }                
        public CompBook CompBook { get; }        
        public CompProperties_Book BookProps { get; }

        public BookType Type { get; }

        public string DefName { get; }
        public string ModName { get; }
        public string PackageId { get; }

        public string Title { get; }
        public string FlavorUI { get; }
        public string DescriptionDetailed { get; }

        public float MentalBreakChancePerHour { get; }
        public float JoyFactor { get; }

        public string NameMakerDefName { get; }
        public string DescriptionMakerDefName { get; }
        public float PickWeight { get; }
        public FloatRange AgeYearsRange { get; }
        public float QuestChance { get; }

        public string SkillDefName { get; }

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
