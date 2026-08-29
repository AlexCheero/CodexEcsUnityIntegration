using System.Collections;
using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Views;
using CodexFramework.Utils;
using UnityEngine;

namespace CodexFramework.CodexEcsUnityIntegration
{
    public class ECSPipelineController : Singleton<ECSPipelineController>
    {
        [SerializeField]
        private ECSPipelineBehaviour[] _pipelines;

        private EcsWorld _world;
        private int _currentPipelineIdx;

        public EcsWorld World => _world;
        public bool IsPaused => CurrentPipelineBehaviour.IsPaused;

        public ECSPipelineBehaviour CurrentPipelineBehaviour => _pipelines[_currentPipelineIdx];

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

            foreach (var view in FindObjectsByType<EntityView>(FindObjectsSortMode.None))
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
            if (CurrentPipelineBehaviour.IsPaused)
                CurrentPipelineBehaviour.Unpause();
            else
                CurrentPipelineBehaviour.Pause();
        }

        public void Pause() => CurrentPipelineBehaviour.Pause();
        public void Unpause() => CurrentPipelineBehaviour.Unpause();

        public void CreateEntityWithComponent<T>(T comp = default) => _world.Add(_world.Create(), comp);

        public void ReRunInit()
        {
            CurrentPipelineBehaviour.RunInitSystems();
        }
    }
}