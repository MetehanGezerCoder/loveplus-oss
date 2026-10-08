import React from 'react';
import {AppProviders} from './src/app/AppProviders';
import {AppNavigator} from './src/app/AppNavigator';

export default function App() {
  return (
    <AppProviders>
      <AppNavigator />
    </AppProviders>
  );
}
