using Ustas.RimAI.Art.Art;
using Ustas.RimAI.Art.Events;
using Ustas.RimAI.Art.Events.Quests;
using Ustas.RimAI.Art.Scanner.Production;
using Ustas.RimAI.Art.Synopsis;
using Verse;

namespace Ustas.RimAI.Art
{
    public sealed class LiteratureGameComponent : GameComponent
    {
        public LiteratureGameComponent()
        {
        }

        public LiteratureGameComponent(Game game)
        {
        }

        public override void GameComponentTick()
        {
            BookSynopsisProcessor.Tick();
            ArtDescriptionProcessor.Tick();
            LetterEventScheduler.Tick();
            QuestEventScheduler.Tick();
            QuestDescriptionRewriter.Tick();
            IdeoDescriptionRewriter.Tick();
            LetterTextRewriter.Tick();
        }
    }
}
