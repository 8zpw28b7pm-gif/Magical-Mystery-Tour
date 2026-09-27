using System;

namespace BlenderInstancePipeline.EditorTools
{
    // Spejler JSON-formatet fra unity_instance_exporter.py (læses med JsonUtility,
    // så feltnavnene skal matche præcis).

    [Serializable]
    public class BlenderInstanceFile
    {
        public const string ExpectedFormat = "blender-instances";
        public const int ExpectedStride = 10; // px,py,pz, qw,qx,qy,qz, sx,sy,sz

        public string format;
        public int version;
        public string source_file;
        public string exported_utc;
        public float unit_scale;
        public string layout;
        public int stride;
        public int total_instances;
        public BlenderInstanceGroup[] groups;
    }

    [Serializable]
    public class BlenderInstanceGroup
    {
        public string source; // kildeobjektets navn i Blender, fx "Tree_Oak" eller "Grass_Tall.001"
        public int count;
        public float[] data;  // count * stride floats
    }
}
