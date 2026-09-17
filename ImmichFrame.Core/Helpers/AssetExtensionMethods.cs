using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;

namespace ImmichFrame.Core.Helpers
{
    public static class AssetExtensionMethods
    {
        public static bool IsSupportedAsset(this AssetResponseDto asset)
        {
            return asset.Type == AssetTypeEnum.IMAGE || asset.Type == AssetTypeEnum.VIDEO;
        }

        public static async Task<IEnumerable<AssetResponseDto>> ApplyAccountFilters(this Task<IEnumerable<AssetResponseDto>> unfilteredAssets, IAccountSettings accountSettings, IReadOnlySet<Guid> excludedAssetIds)
        {
            return ApplyAccountFilters(await unfilteredAssets, accountSettings, excludedAssetIds);
        }

        public static IEnumerable<AssetResponseDto> ApplyAccountFilters(this IEnumerable<AssetResponseDto> unfilteredAssets, IAccountSettings accountSettings, IReadOnlySet<Guid> excludedAssetIds)
        {
            // Display supported media types
            var assets = unfilteredAssets.Where(asset => asset.IsSupportedAsset());

            if (!accountSettings.ShowVideos)
                assets = assets.Where(x => x.Type == AssetTypeEnum.IMAGE);

            if (!accountSettings.ShowArchived)
                assets = assets.Where(x => x.IsArchived == false);

            var takenBefore = accountSettings.ImagesUntilDate.HasValue ? accountSettings.ImagesUntilDate : null;
            if (takenBefore.HasValue)
            {
                assets = assets.Where(x => x.ExifInfo?.DateTimeOriginal != null && x.ExifInfo.DateTimeOriginal <= takenBefore);
            }

            var takenAfter = accountSettings.ImagesFromDate.HasValue ? accountSettings.ImagesFromDate : accountSettings.ImagesFromDays.HasValue ? DateTime.Today.AddDays(-accountSettings.ImagesFromDays.Value) : null;
            if (takenAfter.HasValue)
            {
                assets = assets.Where(x => x.ExifInfo?.DateTimeOriginal != null && x.ExifInfo.DateTimeOriginal >= takenAfter);
            }

            if (accountSettings.Rating is int rating)
            {
                assets = assets.Where(x => x.ExifInfo?.Rating == rating);
            }

            if (excludedAssetIds.Count > 0)
            {
                assets = assets.Where(x => !excludedAssetIds.Contains(x.Id));
            }

            return assets;
        }
    }
}
