using Steamworks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PlayVoice.Pages.Workshop;

internal class WorkshopAuthorInfo
{
    public string Name { get; private set; } = string.Empty;
    public ImageSource Avatar { get; private set; } = null;

    public static async Task<WorkshopAuthorInfo> LoadAsync(SteamId steamId)
    {
        SteamFriends.RequestUserInformation(steamId, true);
        var avatar = await SteamFriends.GetSmallAvatarAsync(steamId);
        var friend = new Friend(steamId);

        return new WorkshopAuthorInfo
        {
            Name = friend.Name,
            Avatar = avatar.HasValue ? ToBitmapSource(avatar.Value) : null
        };
    }

    private static BitmapSource ToBitmapSource(Steamworks.Data.Image image)
    {
        byte[] bgra = new byte[image.Data.Length];
        for (int i = 0; i < image.Data.Length; i += 4)
        {
            bgra[i] = image.Data[i + 2];
            bgra[i + 1] = image.Data[i + 1];
            bgra[i + 2] = image.Data[i];
            bgra[i + 3] = image.Data[i + 3];
        }

        var bitmap = BitmapSource.Create(
            (int)image.Width,
            (int)image.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            bgra,
            (int)image.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }
}
