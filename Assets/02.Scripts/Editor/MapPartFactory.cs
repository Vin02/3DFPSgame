using UnityEngine;

// 맵을 이루는 기본 부품(상자, 경사로, 조명)을 만든다.
public static class MapPartFactory
{
    private static readonly Vector3 LampFixtureSize = new Vector3(1.6f, 0.12f, 0.5f);
    private static readonly Color LampLightColor = new Color(1f, 0.93f, 0.8f);
    private const float LampLightRange = 16f;
    private const float LampLightIntensity = 3f;

    public static Transform CreateGroup(Transform parent, string name)
    {
        var group = new GameObject(name);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    public static GameObject CreateBox(Transform parent, string name, Vector3 center, Vector3 size, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = center;
        box.transform.localScale = size;
        box.GetComponent<MeshRenderer>().sharedMaterial = material;
        return box;
    }

    // 최소 모서리와 최대 모서리로 상자를 만든다. (벽, 바닥처럼 범위로 생각하는 부품용)
    public static GameObject CreateBoxFromBounds(Transform parent, string name, Vector3 min, Vector3 max, Material material)
    {
        return CreateBox(parent, name, (min + max) * 0.5f, max - min, material);
    }

    public static GameObject CreateCylinder(Transform parent, string name, Vector3 baseCenter, float radius, float height, Material material)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.position = baseCenter + Vector3.up * (height * 0.5f);
        cylinder.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        cylinder.GetComponent<MeshRenderer>().sharedMaterial = material;
        return cylinder;
    }

    // XY 평면에서 bottom에서 top으로 오르는 경사로. z는 bottom의 z를 중심으로 width만큼 넓어진다.
    public static GameObject CreateRamp(Transform parent, string name, Vector3 bottom, Vector3 top,
        float width, float thickness, Material material)
    {
        Vector3 slope = top - bottom;
        float angle = Mathf.Atan2(slope.y, slope.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

        // 윗면이 bottom-top을 잇는 선과 맞도록 두께의 절반만큼 아래로 내린다.
        Vector3 center = (bottom + top) * 0.5f - rotation * (Vector3.up * (thickness * 0.5f));
        center.z = bottom.z;

        GameObject ramp = CreateBox(parent, name, center, new Vector3(slope.magnitude, thickness, width), material);
        ramp.transform.rotation = rotation;
        return ramp;
    }

    // 천장에 매달린 등. 빛은 그림자 없이 퍼지게 해 비용을 줄인다.
    public static void CreateLamp(Transform parent, string name, Vector3 position, Material fixtureMaterial)
    {
        GameObject lamp = CreateBox(parent, name, position, LampFixtureSize, fixtureMaterial);
        Object.DestroyImmediate(lamp.GetComponent<Collider>());

        var lightObject = new GameObject("Light", typeof(Light));
        lightObject.transform.SetParent(lamp.transform, false);
        lightObject.transform.localPosition = Vector3.down * 2f;

        var light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = LampLightColor;
        light.range = LampLightRange;
        light.intensity = LampLightIntensity;
        light.shadows = LightShadows.None;
    }
}
