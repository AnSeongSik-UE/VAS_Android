// BlazeFaceDetector.cs
using System;
using Unity.Mathematics;
using Unity.InferenceEngine;
using UnityEngine;

public class BlazeFaceDetector : MonoBehaviour
{
    [SerializeField] private ModelAsset blazeFaceModelAsset;
    [SerializeField] private TextAsset  faceAnchorsCSV;

    private Worker        _worker;
    private Tensor<float> _inputTensor;
    private float[,]     _anchors;

    private const int INPUT_SIZE    = 128;
    private const int NUM_ANCHORS   = 896;
    private const float IOU_THRESH  = 0.3f;
    private const float SCORE_THRESH = 0.5f;

    public struct FaceDetection
    {
        public bool   IsValid;
        public Rect   BoundingBox;
        public float  Score;
        public float2 AnchorPosition;
        public float[] RawBoxes;
    }

    void Start()
    {
        if (blazeFaceModelAsset == null) { Debug.LogWarning("[BlazeFaceDetector] ModelAsset is null"); return; }

        var baseModel = ModelLoader.Load(blazeFaceModelAsset);
        var graph     = new FunctionalGraph();
        var input     = graph.AddInput(baseModel, 0);
        var outputs   = Functional.Forward(baseModel, 2f * input - 1f);
        var rawBoxes  = outputs[0];
        var rawScores = outputs[1];

        var anchorsData = new float[NUM_ANCHORS * 4];
        var anchorsCSV  = faceAnchorsCSV != null ? BlazeUtils.LoadAnchors(faceAnchorsCSV.text, NUM_ANCHORS) : BlazeUtils.LoadAnchors("Data/face_anchors");
        _anchors = anchorsCSV;
        Buffer.BlockCopy(_anchors, 0, anchorsData, 0, anchorsData.Length * sizeof(float));

        var anchorsTensor = Functional.Constant(new TensorShape(NUM_ANCHORS, 4), anchorsData);
        var nmsResult     = BlazeUtils.NMSFiltering(rawBoxes, rawScores, anchorsTensor, INPUT_SIZE, IOU_THRESH, SCORE_THRESH);
        var nmsModel      = graph.Compile(nmsResult.Item1, nmsResult.Item2, nmsResult.Item3);

        _worker      = new Worker(nmsModel, BackendType.CPU);
        _inputTensor = new Tensor<float>(new TensorShape(1, INPUT_SIZE, INPUT_SIZE, 3));
    }

    public async Awaitable<FaceDetection> DetectAsync(WebCamTexture texture)
    {
        if (_worker == null || _inputTensor == null || _anchors == null)
            return new FaceDetection { IsValid = false };

        var size  = Mathf.Max(texture.width, texture.height);
        var scale = size / (float)INPUT_SIZE;
        var M = BlazeUtils.mul(
            BlazeUtils.TranslationMatrix(0.5f * (new Vector2(texture.width, texture.height) + new Vector2(-size, size))),
            BlazeUtils.ScaleMatrix(new Vector2(scale, -scale))
        );

        BlazeUtils.SampleImageAffine(texture, _inputTensor, M);
        _worker.Schedule(_inputTensor);

        var indicesAw = (_worker.PeekOutput(0) as Tensor<int>)?.ReadbackAndCloneAsync();
        var scoresAw  = (_worker.PeekOutput(1) as Tensor<float>)?.ReadbackAndCloneAsync();
        var boxesAw   = (_worker.PeekOutput(2) as Tensor<float>)?.ReadbackAndCloneAsync();

        if (indicesAw == null || scoresAw == null || boxesAw == null) return new FaceDetection { IsValid = false };

        using var outputIndices = await indicesAw;
        using var outputScores  = await scoresAw;
        using var outputBoxes   = await boxesAw;

        if (outputIndices == null || outputScores == null || outputBoxes == null || outputIndices.shape.length == 0)
            return new FaceDetection { IsValid = false };

        int anchorIdx = outputIndices[0];
        if (anchorIdx < 0 || anchorIdx >= NUM_ANCHORS) return new FaceDetection { IsValid = false };

        var anchorPosition = INPUT_SIZE * new float2(_anchors[anchorIdx, 0], _anchors[anchorIdx, 1]);

        float cx = (anchorPosition.x + outputBoxes[0, 0, 0]) / INPUT_SIZE;
        float cy = (anchorPosition.y + outputBoxes[0, 0, 1]) / INPUT_SIZE;
        float w  = outputBoxes[0, 0, 2] / INPUT_SIZE;
        float h  = outputBoxes[0, 0, 3] / INPUT_SIZE;

        float[] rawValues = new float[16];
        for (int i = 0; i < 16; i++) rawValues[i] = outputBoxes[0, 0, i];

        return new FaceDetection
        {
            IsValid        = true,
            BoundingBox    = new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h),
            Score          = outputScores[0, 0, 0],
            AnchorPosition = new float2(_anchors[anchorIdx, 0], _anchors[anchorIdx, 1]),
            RawBoxes       = rawValues
        };
    }

    void OnDestroy() { _worker?.Dispose(); _inputTensor?.Dispose(); }
}
