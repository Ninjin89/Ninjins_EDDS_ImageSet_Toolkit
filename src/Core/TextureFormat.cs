namespace Ninjins_EDDS_ImageSet_Toolkit.Core;

public enum PixelPack
{
	Bgra8,
	Rgba8,
	Dxt1,
	Dxt3,
	Dxt5,
	Bc4,
	Bc5,
	Bc7,
	R8,
	Rg8,
	A8,
	Unknown
}

public enum BlockStore
{
	Lz4,
	Copy
}

public sealed class WriteSettings
{
	public PixelPack Pack { get; set; } = PixelPack.Bgra8;

	/// <summary>0 writes every mip down to 1x1. 1 writes only the base image.</summary>
	public int MaxMips { get; set; } = 1;

	public BlockStore Store { get; set; } = BlockStore.Lz4;

	/// <summary>DXT quality 0-10. 0 is balanced. 8 is high quality.</summary>
	public int Quality { get; set; } = 8;
}

public sealed class PictureInfo
{
	public int Width { get; init; }
	public int Height { get; init; }
	public int MipCount { get; init; }
	public PixelPack Pack { get; init; }
	public string PackLabel { get; init; } = "";
}
