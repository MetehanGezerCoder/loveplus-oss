import React, {type PropsWithChildren, type RefObject} from 'react';
import {SafeAreaView, ScrollView, StyleSheet, type ViewStyle} from 'react-native';
import {colors} from './theme';

export function Screen({children, contentStyle, scrollRef}: PropsWithChildren<{contentStyle?: ViewStyle; scrollRef?: RefObject<ScrollView | null>}>) {
  return (
    <SafeAreaView style={styles.safe}>
      <ScrollView ref={scrollRef} contentContainerStyle={[styles.content, contentStyle]} keyboardShouldPersistTaps="handled">
        {children}
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: {flex: 1, backgroundColor: colors.background},
  content: {flexGrow: 1, padding: 24},
});
