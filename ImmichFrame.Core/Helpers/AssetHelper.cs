// ImmichFrame.Core/Helpers/AssetHelper.cs
using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;

namespace ImmichFrame.Core.Helpers;

public static class AssetHelper
{
    private const int AlbumFetchConcurrency = 4;

    /// <summary>
    /// IDs of the assets that must not be displayed: everything in <see cref="IAccountSettings.ExcludedAlbums"/>
    /// and, when <see cref="IAccountSettings.HideAssetsInOtherAlbums"/> is set, everything in any album
    /// (owned or shared) that is not one of the selected <see cref="IAccountSettings.Albums"/>.
    /// </summary>
    public static async Task<IReadOnlySet<Guid>> GetExcludedAssetIds(ImmichApi immichApi, IAccountSettings accountSettings, CancellationToken ct = default)
    {
        var excludedAlbumIds = new HashSet<Guid>(accountSettings?.ExcludedAlbums ?? new());

        // Without selected albums every album would count as "other", hiding every album asset
        if (accountSettings?.HideAssetsInOtherAlbums == true && accountSettings.Albums?.Count > 0)
        {
            var selectedAlbumIds = accountSettings.Albums.ToHashSet();
            var allAlbums = await immichApi.GetAllAlbumsAsync(null, null, null, null, null, ct);
            excludedAlbumIds.UnionWith(allAlbums.Select(album => album.Id).Where(id => !selectedAlbumIds.Contains(id)));
        }

        var excludedAssetIds = new HashSet<Guid>();
        if (excludedAlbumIds.Count == 0)
        {
            return excludedAssetIds;
        }

        using var throttle = new SemaphoreSlim(AlbumFetchConcurrency);
        var assetIdsPerAlbum = await Task.WhenAll(excludedAlbumIds.Select(async albumId =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                return await GetAlbumAssetIds(immichApi, albumId, ct);
            }
            finally
            {
                throttle.Release();
            }
        }));

        foreach (var assetIds in assetIdsPerAlbum)
        {
            excludedAssetIds.UnionWith(assetIds);
        }

        return excludedAssetIds;
    }

    private static async Task<List<Guid>> GetAlbumAssetIds(ImmichApi immichApi, Guid albumId, CancellationToken ct)
    {
        var assetIds = new List<Guid>();
        int page = 1;
        int batchSize = 1000;
        int itemsInPage;
        do
        {
            var metadataBody = new MetadataSearchDto
            {
                Page = page,
                Size = batchSize,
                AlbumIds = [albumId]
            };
            var searchResponse = await immichApi.SearchAssetsAsync(null, null, metadataBody, ct);

            itemsInPage = searchResponse.Assets?.Items.Count ?? 0;

            if (searchResponse.Assets != null)
            {
                assetIds.AddRange(searchResponse.Assets.Items.Select(asset => asset.Id));
            }

            page++;
        } while (itemsInPage == batchSize);

        return assetIds;
    }
}
