using System;
using UnityEditor.Graphing;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    [Serializable]
    class Matrix2ShaderProperty : MatrixShaderProperty
    {
        public Matrix2ShaderProperty()
        {
            displayName = "Matrix2";
        }

        public override PropertyType propertyType
        {
            get { return PropertyType.Matrix2; }
        }

        public override bool isBatchable
        {
            get { return true; }
        }

        public override string GetPropertyDeclarationString(string delimiter = ";")
        {
            return "float2x2 " + referenceName + " = float2x2(1, 0, 0, 1)" + delimiter;
        }

        public override string GetPropertyAsArgumentString()
        {
            return "float2x2 " + referenceName;
        }

        public override AbstractMaterialNode ToConcreteNode()
        {
            return new Matrix2Node
            {
                row0 = new Vector2(value.m00, value.m01),
                row1 = new Vector2(value.m10, value.m11)
            };
        }

        public override AbstractShaderProperty Copy()
        {
            var copied = new Matrix2ShaderProperty();
            copied.displayName = displayName;
            copied.value = value;
            return copied;
        }
    }
}
