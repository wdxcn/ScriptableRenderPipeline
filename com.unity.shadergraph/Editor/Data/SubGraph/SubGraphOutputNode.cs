using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.ShaderGraph.Drawing;
using UnityEngine;
using UnityEditor.Graphing;
using UnityEditor.Rendering;
using UnityEngine.UIElements;

namespace UnityEditor.ShaderGraph
{
    class SubGraphOutputNode : AbstractMaterialNode, IHasSettings
    {
        static string s_MissingOutputSlot = "A Sub Graph must have at least one output slot";

        public SubGraphOutputNode()
        {
            name = "Output";
        }

        public ShaderStageCapability effectiveShaderStage
        {
            get
            {
                List<MaterialSlot> slots = new List<MaterialSlot>();
                GetInputSlots(slots);

                foreach(MaterialSlot slot in slots)
                {
                    ShaderStageCapability stage = NodeUtils.GetEffectiveShaderStageCapability(slot, true);

                    if(stage != ShaderStageCapability.All)
                        return stage;
                }

                return ShaderStageCapability.All;
            }
        }

        private void ValidateShaderStage()
        {
            List<MaterialSlot> slots = new List<MaterialSlot>();
            GetInputSlots(slots);

            foreach(MaterialSlot slot in slots)
                slot.stageCapability = ShaderStageCapability.All;

            var effectiveStage = effectiveShaderStage;

            foreach(MaterialSlot slot in slots)
                slot.stageCapability = effectiveStage;
        }

        public override void ValidateNode()
        {
            ValidateShaderStage();

            if (!this.GetInputSlots<MaterialSlot>().Any())
            {
                owner.AddValidationError(tempId, s_MissingOutputSlot, ShaderCompilerMessageSeverity.Warning);
            }
            
            base.ValidateNode();
        }

        public int AddSlot(ConcreteSlotValueType concreteValueType)
        {
            var index = this.GetInputSlots<ISlot>().Count() + 1;
            string name = string.Format("Out_{0}", NodeUtils.GetDuplicateSafeNameForSlot(this, index, concreteValueType.ToString()));
            AddSlot(MaterialSlot.CreateMaterialSlot(concreteValueType.ToSlotValueType(), index, name, NodeUtils.GetHLSLSafeName(name), SlotType.Input, Vector4.zero));
            return index;
        }

        protected override void OnSlotsChanged()
        {
            base.OnSlotsChanged();
            ValidateNode();
        }

        public void RemapOutputs(ShaderGenerator visitor, GenerationMode generationMode)
        {
            foreach (var slot in graphOutputs)
                visitor.AddShaderChunk(string.Format("{0} = {1};", slot.shaderOutputName, GetSlotValue(slot.id, generationMode)), true);
        }

        public IEnumerable<MaterialSlot> graphOutputs
        {
            get
            {
                return NodeExtensions.GetInputSlots<MaterialSlot>(this).OrderBy(x => x.id);
            }
        }

        public VisualElement CreateSettingsElement()
        {
            PropertySheet ps = new PropertySheet();
            ps.Add(new ReorderableSlotListView(this, SlotType.Input));
            return ps;
        }
    }
}
