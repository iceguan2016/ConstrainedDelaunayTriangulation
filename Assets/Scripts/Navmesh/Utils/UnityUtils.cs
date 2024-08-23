
namespace Navmesh.Utils
{
    public static class UnityUtils
    {
        public static UnityEngine.Vector3 toVector3(this UnityEngine.Vector2 inVec2, float inY)
        {
            return new UnityEngine.Vector3(inVec2.x, inY, inVec2.y);
        }
    }
}
