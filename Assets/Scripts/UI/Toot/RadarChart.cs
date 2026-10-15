/****************************************************
    文件：RadarChart.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/14 18:12:54
	功能：绘制七维图
*****************************************************/

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class RadarChart : MaskableGraphic
{
    [System.Serializable]
    public struct AttributeData
    {
        public SevenDimension dimension; // 属性名
        [Range(0, 100)]
        public float value;               // 属性数值 (0-100)
    }

    [Header("初始预览数据 (运行时会被 Refresh 覆盖)")]
    public AttributeData[] attributes = new AttributeData[7]
    {
        new AttributeData { dimension = SevenDimension.Reason, value = 80 },
        new AttributeData { dimension = SevenDimension.Courage, value = 55 },
        new AttributeData { dimension = SevenDimension.Piety, value = 35 },
        new AttributeData { dimension = SevenDimension.Tolerance, value = 70 },
        new AttributeData { dimension = SevenDimension.Benevolence, value = 40 },
        new AttributeData { dimension = SevenDimension.Resolve, value = 60 },
        new AttributeData { dimension = SevenDimension.Diligence, value = 40 },
    };

    public float maxValue = 100f;

    [Header("外观设置")]
    public float radius = 150f;
    public int gridLevels = 5;
    public float lineWidth = 2f;

    [Header("颜色设置")]
    public Color gridColor = new Color(0.6f, 0.3f, 0.2f, 1f);
    public Color fillColor = new Color(1f, 0.6f, 0.4f, 0.5f);
    public Color lineColor = new Color(1f, 0.5f, 0.3f, 1f);

    // 缓存 List 避免每次重绘产生 GC
    private List<Vector2> m_DataPoints = new List<Vector2>(7);

    /// <summary>
    /// 暴露给外部的刷新方法：传入运行时的七维字典
    /// </summary>
    /// <param name="stats">运行时维护的七维数据</param>
    public void Refresh(Dictionary<SevenDimension, int> stats)
    {
        if (stats == null || attributes == null || attributes.Length != 7) return;

        // 遍历固定的7个维度，从字典中获取对应的值，确保雷达图顶点顺序不乱
        for (int i = 0; i < attributes.Length; i++)
        {
            SevenDimension dim = attributes[i].dimension;
            if (stats.TryGetValue(dim, out int val))
            {
                attributes[i].value = val;
            }
            else
            {
                attributes[i].value = 0; // 字典中没有该属性时，默认为0
            }
        }

        // 标记顶点数据已变更，触发 Unity 重新调用 OnPopulateMesh
        SetVerticesDirty();
    }

    /// <summary>
    /// 重载方法：直接传入冒险者运行时数据
    /// </summary>
    public void Refresh(AdventurerRuntimeData adventurerData)
    {
        if (adventurerData == null) return;
        Refresh(adventurerData.currentSevenStats);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (attributes == null || attributes.Length != 7) return;

        Vector2 center = new Vector2(rectTransform.rect.width / 2, rectTransform.rect.height / 2);
        float actualRadius = Mathf.Min(radius, Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.45f);

        int sides = 7;
        float angleStep = 360f / sides;
        Vector2[] directions = new Vector2[sides];

        for (int i = 0; i < sides; i++)
        {
            // 起点 -90度 (正下方，即理智)，顺时针增加角度
            float angle = -90f + i * angleStep;
            float rad = angle * Mathf.Deg2Rad;
            directions[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        // ================= 绘制步骤 =================
        // 步骤一：计算数据点并绘制填充区域
        m_DataPoints.Clear();
        for (int i = 0; i < sides; i++)
        {
            float currentValue = Mathf.Clamp(attributes[i].value, 0, maxValue);
            float currentRadius = actualRadius * (currentValue / maxValue);
            m_DataPoints.Add(center + directions[i] * currentRadius);
        }

        int centerIndex = vh.currentVertCount;

        UIVertex centerVert = UIVertex.simpleVert;
        centerVert.color = fillColor;
        centerVert.position = center;
        vh.AddVert(centerVert);

        foreach (var point in m_DataPoints)
        {
            UIVertex vert = UIVertex.simpleVert;
            vert.color = fillColor;
            vert.position = point;
            vh.AddVert(vert);
        }

        for (int i = 0; i < sides; i++)
        {
            int current = centerIndex + 1 + i;
            int next = centerIndex + 1 + ((i + 1) % sides);
            vh.AddTriangle(centerIndex, current, next);
        }

        // 步骤二：绘制背景网格
        for (int level = 1; level <= gridLevels; level++)
        {
            float levelRadius = actualRadius * ((float)level / gridLevels);
            for (int i = 0; i < sides; i++)
            {
                Vector2 start = center + directions[i] * levelRadius;
                Vector2 end = center + directions[(i + 1) % sides] * levelRadius;
                DrawLine(vh, start, end, lineWidth, gridColor);
            }
        }

        for (int i = 0; i < sides; i++)
        {
            Vector2 end = center + directions[i] * actualRadius;
            DrawLine(vh, center, end, lineWidth, gridColor);
        }

        // 步骤三：绘制数据轮廓线
        for (int i = 0; i < sides; i++)
        {
            DrawLine(vh, m_DataPoints[i], m_DataPoints[(i + 1) % sides], lineWidth, lineColor);
        }
    }

    private void DrawLine(VertexHelper vh, Vector2 start, Vector2 end, float width, Color color)
    {
        Vector2 dir = (end - start).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x);
        Vector2 offset = normal * (width / 2f);

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        int index = vh.currentVertCount;

        vertex.position = start - offset; vh.AddVert(vertex);
        vertex.position = start + offset; vh.AddVert(vertex);
        vertex.position = end + offset; vh.AddVert(vertex);
        vertex.position = end - offset; vh.AddVert(vertex);

        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index + 2, index + 3, index);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif
}