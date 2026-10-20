using System.Collections.Generic;
using UnityEngine;

// 아래쪽이 더 통통한 계란 모양 메시를 만든다. (y축을 중심으로 프로필 곡선을 회전시킨 형태)
public static class EggMeshFactory
{
    private const int Segments = 32;
    private const int Rings = 24;

    // 0이면 타원체. 클수록 아래쪽이 통통하고 위쪽이 뾰족해진다.
    private const float Asymmetry = 0.15f;

    // 메시를 새로 만들지 않고 전달받은 메시의 내용을 갱신한다. (이미 이 메시를 쓰는 프리팹이 끊기지 않도록)
    public static void Fill(Mesh mesh, float height, float width)
    {
        float halfHeight = height * 0.5f;
        float radius = width * 0.5f;

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();

        for (int ring = 0; ring <= Rings; ring++)
        {
            float t = Mathf.PI * ring / Rings;
            float sin = Mathf.Sin(t);
            float cos = Mathf.Cos(t);

            float profileRadius = radius * sin * (1f + Asymmetry * cos);
            float profileY = -cos * halfHeight;

            // 프로필 곡선의 접선에서 바깥쪽 법선을 구한다.
            float slopeRadius = radius * (cos * (1f + Asymmetry * cos) - Asymmetry * sin * sin);
            float slopeY = sin * halfHeight;
            Vector2 profileNormal = new Vector2(slopeY, -slopeRadius).normalized;

            for (int segment = 0; segment <= Segments; segment++)
            {
                float angle = 2f * Mathf.PI * segment / Segments;
                float dirX = Mathf.Cos(angle);
                float dirZ = Mathf.Sin(angle);

                vertices.Add(new Vector3(profileRadius * dirX, profileY, profileRadius * dirZ));
                normals.Add(new Vector3(profileNormal.x * dirX, profileNormal.y, profileNormal.x * dirZ));
                uvs.Add(new Vector2((float)segment / Segments, (float)ring / Rings));
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(BuildTriangles(), 0);
        mesh.RecalculateBounds();
    }

    private static int[] BuildTriangles()
    {
        int columns = Segments + 1;
        var triangles = new List<int>();

        for (int ring = 0; ring < Rings; ring++)
        {
            for (int segment = 0; segment < Segments; segment++)
            {
                int a = ring * columns + segment;
                int b = a + columns;
                int c = b + 1;
                int d = a + 1;

                triangles.AddRange(new[] { a, b, c, a, c, d });
            }
        }

        return triangles.ToArray();
    }
}
