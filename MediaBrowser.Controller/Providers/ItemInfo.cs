using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Controller.Providers
{
    public class ItemInfo
    {
        public ItemInfo()
        {
        }

        public ItemInfo(IHasMetadata item)
        {
            Path = item.Path;
            ContainingFolderPath = item.ContainingFolderPath;
            IsInMixedFolder = item.IsInMixedFolder;

            var video = item as Video;
            if (video != null)
            {
                VideoType = video.VideoType;
            }
        }

        public string Path { get; set; }
        public string ContainingFolderPath { get; set; }
        public VideoType VideoType { get; set; }
        public bool IsInMixedFolder { get; set; }
    }
}