//BlazeFaceDetector.cs
using System.IO;
using Unity.Mathematics;
using Unity.InferenceEngine;
using UnityEngine;

public class BlazeFaceDetector : MonoBehaviour
{
    [SerializeField] private ModelAsset blazeFaceModelAsset;

    private Worker _worker;
    private Tensor<float> _inputTensor;
    private float2[] _anchors;

    private const int INPUT_SIZE = 128;

    public struct FaceDetection
    {
        public bool IsValid;
        public Rect BoundingBox;
        public float Score;
        public float2 AnchorPosition;
        public float[] RawBoxes;
    }

    private void Start()
    {
        var model = ModelLoader.Load(blazeFaceModelAsset);

        _worker = new Worker(model, BackendType.GPUCompute);

        _inputTensor = new Tensor<float>(new TensorShape(1, INPUT_SIZE, INPUT_SIZE, 3));

        string csvPath = Path.Combine(Application.streamingAssetsPath, "face_anchors.csv");
        _anchors = BlazeUtils.LoadAnchors(csvPath);
    }

    public async Awaitable<FaceDetection> DetectAsync(WebCamTexture texture)
    {
        var size = Mathf.Max(texture.width, texture.height);
        var scale = size / (float)INPUT_SIZE;
        var M = BlazeUtils.mul(
            BlazeUtils.TranslationMatrix(
                0.5f * (new Vector2(texture.width, texture.height) + new Vector2(-size, size))
                ),
            BlazeUtils.ScaleMatrix(new Vector2(scale, -scale))
            );
    }
}
