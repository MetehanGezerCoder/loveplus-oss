import React, {type PropsWithChildren} from 'react';
import {ActivityIndicator, Pressable, StyleSheet, Text, TextInput, type TextInputProps} from 'react-native';
import {colors} from './theme';

export function Field(props: TextInputProps) {
  return <TextInput placeholderTextColor="#8f8091" {...props} style={[styles.field, props.style]} />;
}

export function PrimaryButton({children, onPress, loading = false, disabled = false}: PropsWithChildren<{
  onPress: () => void;
  loading?: boolean;
  disabled?: boolean;
}>) {
  return (
    <Pressable style={[styles.button, (disabled || loading) && styles.disabled]} onPress={onPress} disabled={disabled || loading}>
      {loading ? <ActivityIndicator color="#21101a" /> : <Text style={styles.buttonText}>{children}</Text>}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  field: {backgroundColor: colors.surface, color: colors.text, borderColor: colors.border, borderWidth: 1, borderRadius: 14, paddingHorizontal: 16, paddingVertical: 13, marginBottom: 12, fontSize: 16},
  button: {backgroundColor: colors.primary, minHeight: 50, borderRadius: 14, alignItems: 'center', justifyContent: 'center', marginTop: 8},
  buttonText: {color: '#21101a', fontWeight: '800', fontSize: 16},
  disabled: {opacity: 0.55},
});
