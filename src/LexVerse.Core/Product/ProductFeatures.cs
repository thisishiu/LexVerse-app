namespace LexVerse.Core.Product;

/// <summary>
/// [Eng]
/// Product capabilities that can be assigned to commercial plans.
/// New capabilities should receive a new stable key instead of reusing an old key.
/// 
/// [Vie]
/// Code này định nghĩa các tính năng sản phẩm có thể được gán cho các gói thương mại (Mapping tương ứng với mã chức năng trên server). 
/// Các tính năng mới nên nhận một khóa ổn định mới thay vì tái sử dụng một khóa cũ.
/// </summary>
public static class ProductFeatures
{
    public static FeatureKey PopupTranslation { get; } = new("translation.popup");

    public static FeatureKey RegionTranslation { get; } = new("translation.region");

    public static FeatureKey FullScreenTranslation { get; } = new("translation.full-screen");

    public static FeatureKey DocumentMode { get; } = new("mode.document");

    public static FeatureKey ComicMode { get; } = new("mode.comic");

    public static FeatureKey CustomTranslationStyle { get; } = new("translation.custom-style");
}
