namespace Assets.Scripts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Assets.Scripts.Flight;
    using ModApi;
    using ModApi.Common;
    using ModApi.Mods;
    using ModApi.Scenes.Events;
    using ModApi.Ui.Inspector;
    using UnityEngine;

    /// <summary>
    /// A singleton object representing this mod that is instantiated and initialize when the mod is loaded.
    /// </summary>
    public class Mod : ModApi.Mods.GameMod
    {
        /// <summary>
        /// Prevents a default instance of the <see cref="Mod"/> class from being created.
        /// </summary>
        private Mod() : base()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the mod object.
        /// </summary>
        /// <value>The singleton instance of the mod object.</value>
        public static Mod Instance { get; } = GetModInstance<Mod>();

        private CloudRenderer cloudRenderer;
        public float[] data;

        protected override void OnModInitialized()
        {
            base.OnModInitialized();

            data = new float[8];

            Game.Instance.SceneManager.SceneLoaded += OnSceneLoaded;
            Game.Instance.UserInterface.AddBuildInspectorPanelAction(InspectorIds.FlightView, OnBuildFlightViewInspectorPanel);
        }

        private void OnSceneLoaded(object sender, SceneEventArgs e)
        {
            if(e.Scene == "Flight")
            {
                cloudRenderer = Game.Instance.FlightScene.ViewManager.GameView.GameCamera.Transform.gameObject.AddComponent<CloudRenderer>();
                cloudRenderer.data.CopyTo(data, 0);
            }
        }

        private void OnBuildFlightViewInspectorPanel(BuildInspectorPanelRequest request)
        {
            GroupModel g = new GroupModel("Clouds");
            
            request.Model.AddGroup(g);

            var densityModel = new SliderModel("Density", () => data[0], s => OnValueChanged(0, s), 0.0001f, 0.01f, false);
            densityModel.ValueFormatter = (f) => FormatValue(f, 4);
            g.Add(densityModel);

            var coverageModel = new SliderModel("Coverage", () => data[1], s => OnValueChanged(1, s), 0.0f, 1.0f, false);
            coverageModel.ValueFormatter = (f) => FormatValue(f, 2);
            g.Add(coverageModel);

            var scaleModel = new SliderModel("Scale", () => data[2], s => OnValueChanged(2, s), 100.0f, 50000.0f, false);
            scaleModel.ValueFormatter = (f) => FormatValue(f, 0);
            g.Add(scaleModel);

            var speedModel = new SliderModel("Cloud Movement Speed", () => data[3], s => OnValueChanged(3, s), -0.1f, 0.1f, false);
            speedModel.ValueFormatter = (f) => FormatValue(f, 2);
            g.Add(speedModel);

            var layerHeightModel = new SliderModel("Layer Height", () => data[4], s => OnValueChanged(4, s), 1000.0f, 25000.0f, false);
            layerHeightModel.ValueFormatter = (f) => FormatValue(f, 0);
            g.Add(layerHeightModel);

            var layerWidthModel = new SliderModel("Layer Spread", () => data[5], s => OnValueChanged(5, s), 100.0f, 15000.0f, false);
            layerWidthModel.ValueFormatter = (f) => FormatValue(f, 0);
            g.Add(layerWidthModel);
            
            var surfRadiusModel = new SliderModel("Surface Radius", () => data[6], s => OnValueChanged(6, s), 1000.0f, 1000000.0f, false);
            surfRadiusModel.ValueFormatter = (f) => FormatValue(f, 0);
            g.Add(surfRadiusModel);

            var maxHeightModel = new SliderModel("Max Cloud Height", () => data[7], s => OnValueChanged(7, s), 1000.0f, 25000.0f, false);
            maxHeightModel.ValueFormatter = (f) => FormatValue(f, 0);
            g.Add(maxHeightModel);
        }

        private string FormatValue(float arg, int decimals) { return arg.ToString("n" + Mathf.Max(0, decimals)); }

        private void OnValueChanged(int index, float value)
        {
            data[index] = value;
            
            if (cloudRenderer != null)
                cloudRenderer.data[index] = value;
        }
    }
}