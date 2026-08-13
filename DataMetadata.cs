namespace PlayVoice;

/// <summary>
/// 持久化数据的通用元数据外壳。
/// </summary>
/// <typeparam name="T">实际保存的数据类型。</typeparam>
internal sealed class DataMetadata<T>
{
    /// <summary>
    /// 数据使用的内部版本索引。
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// 实际数据。
    /// </summary>
    public required T Data { get; init; }
}
