using UnityEditor;
using UnityEditor.SettingsManagement;

namespace UnityEditor.ProBuilder
{
	static class ProBuilderSettingsProvider
	{
		const string k_PreferencesPath = "Preferences/ProBuilder";

#if UNITY_2018_3_OR_NEWER
		[SettingsProvider]
		static SettingsProvider CreateSettingsProvider()
		{
			var provider = new UserSettingsProvider(k_PreferencesPath,
				ProBuilderSettings.instance,
				new [] { typeof(ProBuilderSettingsProvider).Assembly });

			provider.afterSettingsSaved += () =>
			{
				if (ProBuilderEditor.instance != null)
					ProBuilderEditor.instance.OnEnable();
			};

			return provider;
		}
#else

		static UserSettingsProvider s_SettingsProvider;

		[PreferenceItem("ProBuilder")]
		static void ProBuilderPreferencesGUI()
		{
			if(s_SettingsProvider == null)
				s_SettingsProvider = new UserSettingsProvider(ProBuilderSettings.instance, new [] { typeof(ProBuilderSettingsProvider).Assembly });

			s_SettingsProvider.OnGUI(null);
		}
#endif
	}
}