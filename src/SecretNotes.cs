using StardewModdingAPI.Events;
using System.Collections.Generic;

using ichortower.TowerCore;
using INoteData = ichortower.SNF.INoteData;
using ISnfApi = ichortower.SNF.ISnfApi;

namespace ichortower.SNF
{
    public interface ISnfApi
    {
        public INoteData CreateDataObject();
        public bool RegisterSecretNote(string uniqueId, INoteData note);
        public bool Reload(bool data = false, bool conditions = false);
    }

    public interface INoteData
    {
        public string Contents { get; set; }
        public string Title { get; set; }
        public string Conditions { get; set; }
        public string Location { get; set; }
        public string LocationContext { get; set; }
        public string ObjectId { get; set; }
        public string NoteTexture { get; set; }
        public int NoteTextureIndex { get; set; }
        public string NoteTextColor { get; set; }
        public string NoteImageTexture { get; set; }
        public int NoteImageTextureIndex { get; set; }
        public List<string> ActionsOnFirstRead { get; set; }
    }
}

namespace ichortower.MetamorphicCube
{

internal class SecretNotes
{

    internal static void GameLaunched(object sender, GameLaunchedEventArgs e)
    {
        var api = Main.Helper.ModRegistry.GetApi<ISnfApi>("ichortower.SecretNoteFramework");
        if (api is null) {
            return;
        }
        Dictionary<string, Note> fromFile = Main.Helper.ModContent.Load
                <Dictionary<string, Note>>("data/secret-notes.json");
        foreach (var entry in fromFile) {
            string newKey = Tokens.Parse(entry.Key);
            INoteData obj = api.CreateDataObject();
            obj.Title = Tokens.Parse(entry.Value.Title);
            obj.Contents = Tokens.Parse(entry.Value.Contents);
            obj.Conditions = Tokens.Parse(entry.Value.Conditions);
            /* omitting stuff i'm not using
            entry.Value.Location = Tokens.Parse(entry.Value.Location);
            entry.Value.LocationContext = Tokens.Parse(entry.Value.LocationContext);
            entry.Value.ObjectId = Tokens.Parse(entry.Value.ObjectId);
            entry.Value.NoteTexture = Tokens.Parse(entry.Value.NoteTexture);
            entry.Value.NoteTextColor = Tokens.Parse(entry.Value.NoteTextColor);
            entry.Value.NoteImageTexture = Tokens.Parse(entry.Value.NoteImageTexture);
            for (int i = 0; i < entry.Value.ActionsOnFirstRead.Count; ++i) {
                entry.Value.ActionsOnFirstRead[i] = Tokens.Parse(entry.Value.ActionsOnFirstRead[i]);
            }
            */
            api.RegisterSecretNote(newKey, obj);
        }

    }
}

internal class Note : INoteData
{
    public string Contents { get; set; }
    public string Title { get; set; }
    public string Conditions { get; set; }
    public string Location { get; set; }
    public string LocationContext { get; set; }
    public string ObjectId { get; set; }
    public string NoteTexture { get; set; }
    public int NoteTextureIndex { get; set; }
    public string NoteTextColor { get; set; }
    public string NoteImageTexture { get; set; }
    public int NoteImageTextureIndex { get; set; }
    public List<string> ActionsOnFirstRead { get; set; }
}

}
