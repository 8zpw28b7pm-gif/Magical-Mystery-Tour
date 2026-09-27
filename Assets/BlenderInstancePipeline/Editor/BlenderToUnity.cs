using System;
using UnityEngine;

namespace BlenderInstancePipeline.EditorTools
{
    /// <summary>En Blender-akse med fortegn, fx NegY = "-Y i Blender".</summary>
    public enum SignedAxis { PosX, PosY, PosZ, NegX, NegY, NegZ }

    public enum AxisPreset
    {
        [InspectorName("FBX standard (Forward: -Z, Up: Y)  →  (-x, z, -y)")]
        FbxDefault,
        [InspectorName("FBX Forward: Z, Up: Y  →  (x, z, y)")]
        FbxForwardZ,
        [InspectorName("Brugerdefineret")]
        Custom,
    }

    /// <summary>
    /// Beskriver hvilken Blender-akse hver Unity-akse kommer fra.
    ///   unityX = Blender-aksen der bliver til Unity X, osv.
    /// Eksempel (FBX standard): unityX = NegX, unityY = PosZ, unityZ = NegY
    ///   => Unity (x, y, z) = (-Blender x, Blender z, -Blender y)
    /// </summary>
    [Serializable]
    public struct AxisMapping
    {
        public SignedAxis unityX;
        public SignedAxis unityY;
        public SignedAxis unityZ;

        public AxisMapping(SignedAxis x, SignedAxis y, SignedAxis z) { unityX = x; unityY = y; unityZ = z; }

        public static AxisMapping FromPreset(AxisPreset preset, AxisMapping custom) => preset switch
        {
            AxisPreset.FbxDefault => new AxisMapping(SignedAxis.NegX, SignedAxis.PosZ, SignedAxis.NegY),
            AxisPreset.FbxForwardZ => new AxisMapping(SignedAxis.PosX, SignedAxis.PosZ, SignedAxis.PosY),
            _ => custom,
        };

        /// <summary>
        /// Determinanten af mapping-matricen M:
        ///   -1 = gyldig (skifter håndethed, som Blender→Unity kræver)
        ///   +1 = spejlvendt resultat (højrehåndet data tolket venstrehåndet)
        ///    0 = ugyldig (samme Blender-akse brugt to gange)
        /// </summary>
        public int Determinant
        {
            get
            {
                var m = ToMatrix();
                float det = m.m00 * (m.m11 * m.m22 - m.m12 * m.m21)
                          - m.m01 * (m.m10 * m.m22 - m.m12 * m.m20)
                          + m.m02 * (m.m10 * m.m21 - m.m11 * m.m20);
                return Mathf.RoundToInt(det);
            }
        }

        /// <summary>3x3-matricen M (i en Matrix4x4) så unityVector = M * blenderVector.</summary>
        public Matrix4x4 ToMatrix()
        {
            var m = Matrix4x4.zero;
            m.m33 = 1f;
            SetRow(ref m, 0, unityX);
            SetRow(ref m, 1, unityY);
            SetRow(ref m, 2, unityZ);
            return m;
        }

        static void SetRow(ref Matrix4x4 m, int row, SignedAxis axis)
        {
            float sign = (int)axis >= 3 ? -1f : 1f;
            m[row, (int)axis % 3] = sign;
        }

        public override string ToString() => $"Unity(x, y, z) = ({Label(unityX)}, {Label(unityY)}, {Label(unityZ)})";

        static string Label(SignedAxis a) => ((int)a >= 3 ? "-" : "") + "xyz"[(int)a % 3];
    }

    /// <summary>
    /// Konvertering fra Blender (Z-up, højrehåndet) til Unity (Y-up, venstrehåndet).
    ///
    /// HVORDAN DET VIRKER
    /// ------------------
    /// Mappingen er en "signeret permutation" M: hver Unity-akse er præcis én Blender-akse,
    /// evt. med minus. For at skifte fra højre- til venstrehåndet skal M have determinant -1
    /// (en spejling).
    ///
    /// Hvorfor (-x, z, -y) som standard?
    ///   Blenders FBX-exporter (Forward: -Z, Up: Y) roterer Blender-data -90° om X:
    ///       FBX = (x, z, -y)
    ///   Unitys FBX-importer spejler derefter X-aksen for at gå fra højre- til venstrehåndet:
    ///       Unity = (-x, z, -y)
    ///   Terræn og prefabs, der er kommet ind via FBX med standardindstillinger, bruger altså
    ///   præcis denne mapping – derfor lander instances korrekt oven på terrænet.
    ///
    /// Position:  p_unity = M * p_blender
    /// Skala:     s_unity[i] = s_blender[den Blender-akse der mappes til Unity-akse i]
    ///            (skala er en længde langs en akse, så fortegnet fra M forsvinder)
    /// Rotation:  R_unity = M * R_blender * M^T   (basisskift af rotationsmatricen)
    ///            For en quaternion (w, v) betyder det:
    ///                w_unity = w
    ///                v_unity = det(M) * (M * v)
    ///            Vektordelen v er en "pseudovektor" (rotationsakse). Under en spejling
    ///            (det = -1) vender den fortegn i forhold til en almindelig vektor – derfor
    ///            faktoren det(M). Med standardmappingen giver det:
    ///                q_unity = (x: qx, y: -qz, z: qy, w: qw)
    ///
    /// HVIS NOGET ER SPEJLET ELLER ROTERET FORKERT
    /// ------------------------------------------
    /// Det skyldes næsten altid andre FBX-akseindstillinger i Blender-exporteren (eller at
    /// "Bake Axis Conversion" er slået til i Unity). Skift preset i importvinduet, eller
    /// vælg "Brugerdefineret" og angiv selv, hvilken Blender-akse hver Unity-akse kommer fra.
    /// Tip: læg et asymmetrisk testobjekt (fx en pil der peger mod Blender +X) både som
    /// almindeligt FBX-objekt og som instance, og sammenlign i Unity.
    /// </summary>
    public static class BlenderToUnity
    {
        public static Vector3 Position(Vector3 blender, AxisMapping m) =>
            new Vector3(Pick(blender, m.unityX), Pick(blender, m.unityY), Pick(blender, m.unityZ));

        public static Vector3 Scale(Vector3 blender, AxisMapping m) =>
            new Vector3(blender[(int)m.unityX % 3], blender[(int)m.unityY % 3], blender[(int)m.unityZ % 3]);

        /// <param name="w">Blender quaternion w</param>
        /// <param name="x">Blender quaternion x</param>
        /// <param name="y">Blender quaternion y</param>
        /// <param name="z">Blender quaternion z</param>
        public static Quaternion Rotation(float w, float x, float y, float z, AxisMapping m)
        {
            Vector3 v = Position(new Vector3(x, y, z), m) * m.Determinant;
            var q = new Quaternion(v.x, v.y, v.z, w);
            return Normalize(q);
        }

        static float Pick(Vector3 v, SignedAxis axis)
        {
            float value = v[(int)axis % 3];
            return (int)axis >= 3 ? -value : value;
        }

        static Quaternion Normalize(Quaternion q)
        {
            float len = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return len > 1e-6f ? new Quaternion(q.x / len, q.y / len, q.z / len, q.w / len) : Quaternion.identity;
        }

        /// <summary>
        /// Kombinerer instance-transformen med prefab'ens egen root-transform.
        ///
        /// Prefabs lavet fra en Blender-FBX har typisk en root-rotation (fx -90° om X) og/eller
        /// skala fra FBX-importen. Hvis vi bare overskrev den med instance-rotationen, ville
        /// træerne ligge ned. I stedet beregnes:
        ///     final = Instance(T, R, S) * PrefabRoot(t, r, s)
        /// dvs. "prefab'en som den står i sin egen fil" svarer til kildeobjektet i Blender
        /// placeret i origo uden rotation – og instance-transformen lægges ovenpå.
        ///
        /// Skala: S * r kan kun udtrykkes eksakt som r * S' når skalaen er uniform eller r er
        /// en 90°-rotation (typisk for FBX). Ellers er resultatet en tilnærmelse.
        /// </summary>
        public static void ComposeWithPrefabRoot(
            Vector3 position, Quaternion rotation, Vector3 scale, Transform prefabRoot,
            out Vector3 outPosition, out Quaternion outRotation, out Vector3 outScale)
        {
            Vector3 pLocalPos = prefabRoot.localPosition;
            Quaternion pRot = prefabRoot.localRotation;
            Vector3 pScale = prefabRoot.localScale;

            outPosition = position + rotation * Vector3.Scale(scale, pLocalPos);
            outRotation = rotation * pRot;

            // S' = diag(r^T * S * r) ⊙ s   (diagonalen af skalaen "set gennem" prefab-rotationen)
            Matrix4x4 r = Matrix4x4.Rotate(pRot);
            var rotatedScale = new Vector3(
                r.m00 * r.m00 * scale.x + r.m10 * r.m10 * scale.y + r.m20 * r.m20 * scale.z,
                r.m01 * r.m01 * scale.x + r.m11 * r.m11 * scale.y + r.m21 * r.m21 * scale.z,
                r.m02 * r.m02 * scale.x + r.m12 * r.m12 * scale.y + r.m22 * r.m22 * scale.z);
            outScale = Vector3.Scale(rotatedScale, pScale);
        }
    }
}
