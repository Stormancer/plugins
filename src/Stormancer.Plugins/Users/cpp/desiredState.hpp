

#include <memory>
#include "stormancer/Tasks.h"
#if !defined(STORM_PLUGIN_IMPL)
#define STORM_PLUGIN_IMPL 0
#endif
#if !defined(STORM_DESIRED_STATE_DEFINITION)
#define STORM_DESIRED_STATE_DEFINITION

// Public API

namespace Stormancer
{
	struct StateId
	{
		StateId(const char* id) : id(id) {}
		std::string id;

		bool operator==(const StateId& other) const
		{
			return id == other.id;
		}
	};

	
	class DesiredState : public std::enable_shared_from_this<DesiredState>
	{
		friend class DesiredStateApiImpl;
	public:
		virtual const StateId& id() const = 0;
		virtual bool isStateReached() const = 0;
	
		virtual ~DesiredState()  {}
		
		virtual std::string desiredStateId() const
		{
			return id().id;
		}

		pplx::task<void> waitAsync(pplx::cancellation_token ct)
		{
			if (isStateReached())
			{
				return pplx::task_from_result();
			}
			else
			{
				return pplx::create_task(_stateReachedTce);
			}
		}
	protected:
		virtual void initialize() = 0;

		void notifyStateReached()
		{
			_stateReachedTce.set();
		}

		/// <summary>
		/// Notifies a non recoverable error: the state connot be reached.
		/// </summary>
		/// <param name="exception"></param>
		void notifyError(const std::exception& exception)
		{
			_stateReachedTce.set_exception(exception);
		}

		/// <summary>
		/// Notifies that the state has been lost.
		/// </summary>
		void reset()
		{
			if (_stateReachedTce._IsTriggered())
			{
				_stateReachedTce = {};
			}
		}
		pplx::task_completion_event<void> _stateReachedTce;
	};

	class DesiredStateBuilder
	{
		friend class DesiredStateApiImpl;
	public:
		DesiredStateBuilder& factory(std::function<std::shared_ptr<DesiredState>(const StateId&)> f)
		{
			_factory = f;
			return *this;
		}

		DesiredStateBuilder& addDependency(const StateId& dependencyId)
		{
			_dependencies.push_back(dependencyId);
			return *this;
		}
	private:
		std::function<std::shared_ptr<DesiredState>(const StateId&)> _factory;

		std::vector<StateId> _dependencies;
	};

	/// <summary>
	/// Api enabling game programmer to define the online state they desire the game to be in. 
	/// The library then internally tries to reach this state and provides feedback to the game code.
	/// </summary>
	class DesiredStateApi
	{
	public:
		virtual std::shared_ptr<DesiredState> activateDesiredState(const StateId& id,const std::function<void(std::shared_ptr<DesiredState>)>& initializer) = 0;

		virtual void configureState(const StateId& id,std::function<void(DesiredStateBuilder&)> configurator) = 0;
	};

}
#endif
#if STORM_PLUGIN_IMPL == 1
#if !defined(STORM_DESIRED_STATE_IMPL)
#define STORM_DESIRED_STATE_IMPL
#include <unordered_map>
#include <mutex>
#include "stormancer/IPlugin.h"
// Private impl

template<>
struct std::hash<Stormancer::StateId>
{
	std::size_t operator()(const Stormancer::StateId& k) const
	{
		return std::hash<std::string>()(k.id);
	}
};


namespace Stormancer
{
	class DesiredStateApiImpl : public DesiredStateApi
	{
	public:
		virtual ~DesiredStateApiImpl(){}
		void configureState(const StateId& id, std::function<void(DesiredStateBuilder&)> configurator) override
		{
			DesiredStateBuilder builder;
			configurator(builder);
			_builders.emplace(id, builder);
		}

		std::shared_ptr<DesiredState> activateDesiredState(const StateId& id,const std::function<void(std::shared_ptr<DesiredState>)>& initializer) override
		{
			std::lock_guard<std::mutex> lg(_mutex);
			return activateDesiredStateImpl(id, initializer);
			
		}

		std::shared_ptr<DesiredState> activateDesiredStateImpl(const StateId& id,const std::function<void(std::shared_ptr<DesiredState>)>& initializer)
		{
			auto state = createState(id, initializer);
			auto stateId = state->desiredStateId();
			auto it = _activeStates.find(stateId);
			std::weak_ptr<DesiredState> wState;
			if (it != _activeStates.end())
			{
				wState = it->second;
			}

			if (wState.expired())
			{
				_activeStates.erase(stateId);
			
				
				if (state)
				{
					
					_activeStates.emplace(stateId, state->weak_from_this());
					try
					{
						state->initialize();
					}
					catch (...)
					{
						_activeStates.erase(stateId);
						throw;
					}
				}
				return state;
			}
			else
			{
				return wState.lock();
			}
		}

	private:
		std::mutex _mutex;
		std::unordered_map<std::string, std::weak_ptr<DesiredState>> _activeStates;
		std::unordered_map<StateId, DesiredStateBuilder> _builders;

		std::shared_ptr<DesiredState> createState(const StateId& id, std::function<void(std::shared_ptr<DesiredState>)> initializer)
		{
			auto it = _builders.find(id);
			if (it == _builders.end())
			{
				return nullptr;
			}
			
			auto state = it->second._factory(id);
			initializer(state);
			
			return state;
		}
	};

	class DesiredStatePlugin : public IPlugin
	{
		// Inherited via IPlugin
		PluginDescription getDescription() override
		{
			return PluginDescription("DesiredState", "1.0.0");
		}

		void registerClientDependencies(ContainerBuilder& builder) override
		{
			builder.registerDependency<DesiredStateApi>([](const DependencyScope& dr) {
				return std::make_shared<DesiredStateApiImpl>();
				}).singleInstance();
		}
	};
}
#endif
#endif