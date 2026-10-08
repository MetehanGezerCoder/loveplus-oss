import React from 'react';
import {DarkTheme, NavigationContainer} from '@react-navigation/native';
import {createNativeStackNavigator} from '@react-navigation/native-stack';
import {BootstrapScreen, ConnectionErrorScreen} from '../features/bootstrap';
import {LoginScreen, RegisterScreen, useAuthStore} from '../features/auth';
import {CreatePairingCodeScreen, PairingHomeScreen, RedeemPairingCodeScreen, useCurrentPair} from '../features/pairing';
import {DashboardScreen} from '../features/dashboard';
import {DiagnosticsScreen} from '../features/diagnostics';
import {PermissionsScreen} from '../features/permissions';
import {SettingsScreen} from '../features/settings';
import {colors} from '../shared/ui/theme';

export type RootStackParams = {
  Login: undefined; Register: undefined; PairingHome: undefined; CreatePairingCode: undefined;
  RedeemPairingCode: undefined; Dashboard: undefined; Permissions: undefined; Settings: undefined;
  Diagnostics: undefined;
};
const Stack = createNativeStackNavigator<RootStackParams>();
const screenOptions = {headerStyle: {backgroundColor: colors.background}, headerTintColor: colors.text, contentStyle: {backgroundColor: colors.background}};

export function AppNavigator() {
  const {bootstrapped, session} = useAuthStore();
  if (!bootstrapped) return <BootstrapScreen />;
  return (
    <NavigationContainer theme={{...DarkTheme, colors: {...DarkTheme.colors, background: colors.background, card: colors.background, primary: colors.primary}}}>
      {!session ? <GuestNavigator /> : <SignedInNavigator />}
    </NavigationContainer>
  );
}

function GuestNavigator() {
  return <Stack.Navigator screenOptions={{...screenOptions, headerShown: false}}><Stack.Screen name="Login" component={LoginScreen} /><Stack.Screen name="Register" component={RegisterScreen} /></Stack.Navigator>;
}

function SignedInNavigator() {
  const pair = useCurrentPair();
  if (pair.isLoading) return <BootstrapScreen label="Eşleşme bilgisi alınıyor…" />;
  // An unreachable API used to fall through to the pairing screen, which reported "no partner"
  // for what was actually a connectivity or build-environment failure.
  if (pair.isError) {
    return (
      <ConnectionErrorScreen
        message={pair.error instanceof Error ? pair.error.message : undefined}
        retrying={pair.isFetching}
        onRetry={() => {
          pair.refetch().catch(() => undefined);
        }}
      />
    );
  }
  return (
    <Stack.Navigator screenOptions={screenOptions}>
      {pair.data ? <Stack.Screen name="Dashboard" component={DashboardScreen} options={{headerShown: false}} /> : <Stack.Screen name="PairingHome" component={PairingHomeScreen} options={{headerShown: false}} />}
      {!pair.data ? <><Stack.Screen name="CreatePairingCode" component={CreatePairingCodeScreen} options={{title: 'Kod oluştur'}} /><Stack.Screen name="RedeemPairingCode" component={RedeemPairingCodeScreen} options={{title: 'Kod gir'}} /></> : null}
      <Stack.Screen name="Permissions" component={PermissionsScreen} options={{title: 'İzinler'}} />
      <Stack.Screen name="Settings" component={SettingsScreen} options={{title: 'Ayarlar'}} />
      <Stack.Screen name="Diagnostics" component={DiagnosticsScreen} options={{title: 'Tanılama'}} />
    </Stack.Navigator>
  );
}
