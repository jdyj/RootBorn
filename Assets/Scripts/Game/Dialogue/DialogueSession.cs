namespace Rootborn.Game.Dialogue
{
    public sealed class DialogueSession
    {
        public DialogueDefinition Current { get; private set; }
        public bool IsOpen => Current != null;

        public void Open(DialogueDefinition dialogue)
        {
            Current = dialogue;
        }

        public void Close()
        {
            Current = null;
        }
    }
}
