//━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━																												
// Copyright 2020, Alexander Ameye, All rights reserved.
// https://alexander-ameye.gitbook.io/stylized-water/
//━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━	

// [수정] Unity 6 / URP 17 대응.
// URP 17에서 ScriptableRenderPass.Execute(ScriptableRenderContext, ref RenderingData)가
// 제거되고 RenderGraph의 RecordRenderGraph로 대체되었다. 그대로 두면 컴파일되지 않는다.
// 그리는 내용(수면 높이에 큰 쿼드를 깔고 코스틱 머티리얼로 칠하기)은 원본과 같다.

#if UNIVERSAL_RENDERER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace StylizedWater
{
    public class CausticsPass : ScriptableRenderPass
    {
        private const string profilerTag = "Caustics Pass";

        public Material causticsMaterial;
        private static Mesh mesh;
        private float waterLevel;

        private const float BIAS = 0.1f;

        public CausticsPass(float waterLevel)
        {
            this.waterLevel = waterLevel;
        }

        /// <summary>RenderGraph 패스가 실행 시점에 쓸 값들.</summary>
        private class PassData
        {
            public Mesh mesh;
            public Material material;
            public Matrix4x4 matrix;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!causticsMaterial) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var cam = cameraData.camera;
            if (cam == null || cam.cameraType == CameraType.Preview) return;

            var resourceData = frameData.Get<UniversalResourceData>();

            // 태양 방향은 머티리얼 전역 값이라 패스 밖에서 한 번만 넣는다.
            var sunMatrix = RenderSettings.sun != null
                        ? RenderSettings.sun.transform.localToWorldMatrix
                        : Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(-45f, 45f, 0f), Vector3.one);
            causticsMaterial.SetMatrix("_MainLightDirection", sunMatrix);

            if (!mesh) mesh = GenerateQuad(1000f);

            // 카메라가 수면 위면 수면에, 아래면 카메라 살짝 아래에 깐다.
            var position = cam.transform.position;
            position.y = position.y > waterLevel ? waterLevel : position.y - BIAS;

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(profilerTag, out var passData))
            {
                passData.mesh = mesh;
                passData.material = causticsMaterial;
                passData.matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);

                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawMesh(data.mesh, data.matrix, data.material, 0, 0);
                });
            }
        }

        private static Mesh GenerateQuad(float size)
        {
            var m = new Mesh();

            size *= 0.5f;

            var verts = new[]
            {
                new Vector3(-size, 0f, -size),
                new Vector3(size, 0f, -size),
                new Vector3(-size, 0f, size),
                new Vector3(size, 0f, size)
            };

            var tris = new[]
            {
                0, 2, 1,
                2, 3, 1
            };

            m.vertices = verts;
            m.triangles = tris;

            return m;
        }
    }
}
#endif