
namespace Navmesh
{
    public class FTiledNavmeshBuilder
    { 

        struct FTiledNavmeshBuilderParams
        {
            public UnityEngine.Vector3 MinBounds;
            public UnityEngine.Vector3 MaxBounds;

            public float TileSize;
        }

        bool CalcGridSize(UnityEngine.Vector3 InMinBounds, UnityEngine.Vector3 InMaxBounds, float InTileSize, out int OutSizeX, out int OutSizeZ)
        {
	        OutSizeX = (int) ((InMaxBounds[0] - InMinBounds[0]) / InTileSize + 0.5f);
	        OutSizeZ = (int) ((InMaxBounds[2] - InMinBounds[2]) / InTileSize + 0.5f);
            return true;
        }

        FTiledNavmeshData BuildAllTiles(FTiledNavmeshBuilderParams InParams)
        {
            FTiledNavmeshData NavMeshData = null;
            if (CalcGridSize(InParams.MinBounds, InParams.MaxBounds, InParams.TileSize, out var OutSizeX, out var OutSizeZ))
            {
                NavMeshData = new FTiledNavmeshData();

                var TileMinBounds = UnityEngine.Vector3.zero;
                var TileMaxBounds = UnityEngine.Vector3.zero;
                for (int z = 0; z < OutSizeZ; ++z)
                {
                    for (int x = 0; x < OutSizeX; ++x)
                    {
                        TileMinBounds[0] = InParams.MinBounds[0] + x * InParams.TileSize;
                        TileMinBounds[1] = InParams.MinBounds[1];
                        TileMinBounds[2] = InParams.MinBounds[2] + z * InParams.TileSize;

                        TileMaxBounds[0] = InParams.MinBounds[0] + (x+1) * InParams.TileSize;
                        TileMaxBounds[1] = InParams.MaxBounds[1];
                        TileMaxBounds[2] = InParams.MinBounds[2] + (z+1) * InParams.TileSize;

                        var TileData = BuildTileMesh(x, z, TileMinBounds, TileMaxBounds);
                        if (null != TileData)
                        {
                            NavMeshData.RemoveTileDataAt(x, z);
                            NavMeshData.AddTileData(TileData);
                        }
                    }
                }
            }
            return NavMeshData;
        }

        FTileData BuildTileMesh(int InTileX, int InTileZ, UnityEngine.Vector3 InTileMinBounds, UnityEngine.Vector3 InTileMaxBounds)
        {

            return null;
        }
    }
}
