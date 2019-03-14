using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.Rendering.LookDev
{
    public class ToolbarRadio : Toolbar, INotifyValueChanged<int>
    {
        public new class UxmlFactory : UxmlFactory<ToolbarRadio, UxmlTraits> { }
        public new class UxmlTraits : Button.UxmlTraits { }

        List<ToolbarToggle> radios = new List<ToolbarToggle>();

        public new static readonly string ussClassName = "unity-toolbar-radio";

        public int radioLength { get; private set; } = 0;

        int m_Value;
        public int value
        {
            get => m_Value;
            set
            {
                if (value == m_Value)
                    return;

                if (panel != null)
                {
                    using (ChangeEvent<int> evt = ChangeEvent<int>.GetPooled(m_Value, value))
                    {
                        evt.target = this;
                        SetValueWithoutNotify(value);
                        SendEvent(evt);
                    }
                }
                else
                {
                    SetValueWithoutNotify(value);
                }
            }
        }

        public ToolbarRadio()
        {
            RemoveFromClassList(Toolbar.ussClassName);
            AddToClassList(ussClassName);
        }

        public void AddRadio(string text)
        {
            var toggle = new ToolbarToggle();
            toggle.RegisterValueChangedCallback(InnerValueChanged(radioLength));
            toggle.text = text;
            toggle.SetValueWithoutNotify(radioLength == 0);
            radios.Add(toggle);
            Add(toggle);
            radioLength++;
        }

        public void AddRadios(string[] texts)
        {
            foreach (var text in texts)
                AddRadio(text);
        }

        EventCallback<ChangeEvent<bool>> InnerValueChanged(int radioIndex)
        {
            return (ChangeEvent<bool> evt) =>
            {
                if (radioIndex == m_Value)
                {
                    if (!evt.newValue)
                        radios[radioIndex].SetValueWithoutNotify(true);
                }
                else
                    value = radioIndex;
            };
        }

        public void SetValueWithoutNotify(int newValue)
        {
            if (m_Value != newValue)
            {
                if (newValue < 0 || newValue >= radioLength)
                    throw new System.IndexOutOfRangeException();

                radios[m_Value].SetValueWithoutNotify(false);
                radios[newValue].SetValueWithoutNotify(true);

                m_Value = newValue;
            }
        }
    }
}
