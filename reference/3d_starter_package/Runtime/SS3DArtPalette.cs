using UnityEngine;

namespace ShadowSteps.Art3D
{
    [CreateAssetMenu(menuName="Shadow Steps/3D Art Palette",fileName="SS3DArtPalette")]
    public class SS3DArtPalette : ScriptableObject
    {
        [Header("Grid geometry (one tile = one Unity unit)")]
        public GameObject floorA, floorB, wallBlock, wallStraight, planter, crate, stoneBlock;
        [Header("Interactables")]
        public GameObject node, exitPad, collectibleKey, lockedDoor, openedDoor;
        [Header("Actors and wall-mounted security cameras")]
        public GameObject spyProxy, guardProxy, cameraModel, cameraMount, cameraHead;
        [Header("Optional decorative pieces; not placed onto gameplay nodes")]
        public GameObject archFrame, palaceDoor, stairs, pillar, water, bench, lantern, banner, flowerbed;
    }
}
