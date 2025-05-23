using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Views;
using CodexFramework.Utils;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration
{
    public class ECSPipelineController : Singleton<ECSPipelineController>
    {
        [SerializeField]
        private ECSPipeline[] _pipelines;

        private EcsWorld _world;
        private int _currentPipelineIdx;

        public EcsWorld World => _world;
        public bool IsPaused => CurrentPipeline.IsPaused;

        public ECSPipeline CurrentPipeline => _pipelines[_currentPipelineIdx];

        //previously was void Start()
        protected override void Init()
        {
            base.Init();
            
            _world = new EcsWorld();

            foreach (var pipeline in _pipelines)
            {
                pipeline.Init(_world);
                pipeline.Switch(false);
            }

            foreach (var view in FindObjectsOfType<EntityView>(true))
            {
                if (view.gameObject.activeSelf || view.ForceInit)
                    view.InitAsEntity(_world);
            }

            SwitchPipeline(0);
        }

        public void SwitchPipeline(int idx)
        {
#if DEBUG
            if (idx < 0 || idx >= _pipelines.Length)
            {
                Debug.LogError("pipeline index out of range");
                return;
            }
#endif

            _currentPipelineIdx = idx;
            for (int i = 0; i < _pipelines.Length; i++)
                _pipelines[i].Switch(i == idx);
        }

        public void TogglePause()
        {
            if (CurrentPipeline.IsPaused)
                CurrentPipeline.Unpause();
            else
                CurrentPipeline.Pause();
        }

        public void Pause() => CurrentPipeline.Pause();
        public void Unpause() => CurrentPipeline.Unpause();

        public void CreateEntityWithComponent<T>(T comp = default) => _world.Add(_world.Create(), comp);

        public void ReRunInit()
        {
            CurrentPipeline.RunInitSystems();
        }
    }
}