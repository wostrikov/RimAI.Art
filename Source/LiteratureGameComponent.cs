using Ustas.RimAI.Art.art;
using Ustas.RimAI.Art.events;
using Ustas.RimAI.Art.events.quests;
using Ustas.RimAI.Art.scanner.production;
using Ustas.RimAI.Art.synopsis;
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
