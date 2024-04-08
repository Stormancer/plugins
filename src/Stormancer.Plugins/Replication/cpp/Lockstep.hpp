#pragma once
#include "stormancer/IPlugin.h"

#if !defined(STRM_PLUGIN_IMPL)
#define STRM_PLUGIN_IMPL 1
#endif

namespace Stormancer
{
	namespace Gameplay
	{
		class LockstepApi
		{
			void tick();
		};

		class LockstepPlugin : public IPlugin
		{

		};
	}
}


#if STRM_PLUGIN_IMPL == 1

namespace Stormancer
{
	namespace Gameplay
	{
		namespace details
		{
			class LockstepService
			{

			};
		}
		class LockstepApi
		{
			void tick()
			{

			}
		};



	}
}


#endif