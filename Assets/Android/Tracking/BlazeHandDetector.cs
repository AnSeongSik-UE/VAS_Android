// BlazeHandDetector.cs
using System;
using Unity.Mathematics;
using Unity.InferenceEngine;
using UnityEngine;

public class BlazeHandDetector : MonoBehaviour
{
    [SerializeField] private ModelAsset handDetectorAsset;
    [SerializeField] private TextAsset  handAnchorsCSV;

    private Worker        _worker;
    private Tensor<float> _inputTensor;
    private float[,]      _anchors;

    private const int DETECTOR_SIZE = 192;
    private const int NUM_ANCHORS   = 2016;
    private const float SCORE_THRESH = 0.5f;

    public struct HandDetection
    {
        public bool     IsValid;
        public float2   CenterTensorSpace;
        public float    SizeTensorSpace;
        public float    Rotation;
        public float2x3 DetectorMatrix;
    }

    void Start()
    {
        if (handDetectorAsset == null) { Debug.LogWarning("[BlazeHandDetector] ModelAsset is null"); return; }

        var baseModel = ModelLoader.Load(handDetectorAsset);
        var graph     = new FunctionalGraph();
        var input     = graph.AddInput(baseModel, 0);
        var outputs   = Functional.Forward(baseModel, input);
        var rawBoxes  = outputs[0];
        var rawScores = outputs[1];

        var argmaxResult = BlazeUtils.ArgMaxFiltering(rawBoxes, rawScores);
        var argmaxModel  = graph.Compile(argmaxResult.Item1, argmaxResult.Item2, argmaxResult.Item3);

        _worker      = new Worker(argmaxModel, BackendType.CPU);
        _inputTensor = new Tensor<float>(new TensorShape(1, DETECTOR_SIZE, DETECTOR_SIZE, 3));

        _anchors = handAnchorsCSV != null ? BlazeUtils.LoadAnchors(handAnchorsCSV.text, NUM_ANCHORS) : BlazeUtils.LoadAnchors("Data/hand_anchors");
    }

    public async Awaitable<HandDetection> DetectAsync(WebCamTexture texture)
    {
        if (_worker == null || _inputTensor == null || _anchors == null)
            return new HandDetection { IsValid = false };

        var size  = Mathf.Max(texture.width, texture.height);
        var scale = size / (float)DETECTOR_SIZE;
        var M = BlazeUtils.mul(
            BlazeUtils.TranslationMatrix(0.5f * (new Vector2(texture.width, texture.height) + new Vector2(-size, size))),
            BlazeUtils.ScaleMatrix(new Vector2(scale, -scale))
        );
        BlazeUtils.SampleImageAffine(texture, _inputTensor, M);
        _worker.Schedule(_inputTensor);

        var t0 = _worker.PeekOutput(0) as Tensor<int>;
        var t1 = _worker.PeekOutput(1) as Tensor<float>;
        var t2 = _worker.PeekOutput(2) as Tensor<float>;

        if (t0 == null || t1 == null || t2 == null) return new HandDetection { IsValid = false };

        using var outputIdx   = await t0.ReadbackAndCloneAsync();
        using var outputScore = await t1.ReadbackAndCloneAsync();
        using var outputBox   = await t2.ReadbackAndCloneAsync();

        if (outputIdx == null || outputScore == null || outputBox == null || outputScore.shape.length == 0)
            return new HandDetection { IsValid = false };

        if (outputScore[0] < SCORE_THRESH) return new HandDetection { IsValid = false };

        int idx = outputIdx[0];
        if (idx < 0 || idx >= NUM_ANCHORS) return new HandDetection { IsValid = false };

        var anchorPosition = DETECTOR_SIZE * new float2(_anchors[idx, 0], _anchors[idx, 1]);
        var boxCentre = anchorPosition + new float2(outputBox[0, 0, 0], outputBox[0, 0, 1]);
        var boxSize   = math.max(outputBox[0, 0, 2], outputBox[0, 0, 3]);

        var kp0 = anchorPosition + new float2(outputBox[0, 0, 4], outputBox[0, 0, 5]);
        var kp2 = anchorPosition + new float2(outputBox[0, 0, 8], outputBox[0, 0, 9]);
        var delta = kp2 - kp0;
        var up    = delta / math.length(delta);
        var theta = math.atan2(delta.y, delta.x);
        var rotation = 0.5f * Mathf.PI - theta;

        var center  = boxCentre + 0.5f * boxSize * up;
        var sizeOut = boxSize * 2.6f;

        return new HandDetection
        {
            IsValid           = true,
            CenterTensorSpace = center,
            SizeTensorSpace   = sizeOut,
            Rotation          = rotation,
            DetectorMatrix    = M
        };
    }

    void OnDestroy() { _worker?.Dispose(); _inputTensor?.Dispose(); }
}
