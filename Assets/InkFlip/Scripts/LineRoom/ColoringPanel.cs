using UnityEngine;

namespace InkFlip
{
    // One colour picture on the wall. ColoringBook fills in the texture and handles looking at it and drawing on it.
    public class ColoringPanel : MonoBehaviour
    {
        [Tooltip("Which picture (0 tree, 1 sun, 2 house, 3 fish, 4 flower).")]
        public int pictureIndex;
        public Renderer panelRenderer;

        public bool IsDone { get; set; }
        public string Title => ColoringPictures.Names[((pictureIndex % ColoringPictures.Count) + ColoringPictures.Count) % ColoringPictures.Count];
    }
}
