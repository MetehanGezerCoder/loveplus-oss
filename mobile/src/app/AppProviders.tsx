import React, {type PropsWithChildren, useEffect} from 'react';
import {AppState, Platform} from 'react-native';
import {focusManager, QueryClientProvider} from '@tanstack/react-query';
import {SafeAreaProvider} from 'react-native-safe-area-context';
import {queryClient} from '../shared/api/queryClient';

export function AppProviders({children}: PropsWithChildren) {
  useEffect(() => {
    if (Platform.OS === 'web') return undefined;

    focusManager.setFocused(AppState.currentState === 'active');
    const subscription = AppState.addEventListener('change', state => {
      focusManager.setFocused(state === 'active');
    });

    return () => subscription.remove();
  }, []);

  return (
    <SafeAreaProvider>
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    </SafeAreaProvider>
  );
}
