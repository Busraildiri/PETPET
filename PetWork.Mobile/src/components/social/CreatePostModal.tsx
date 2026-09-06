import { Ionicons } from '@expo/vector-icons';
import * as ImagePicker from 'expo-image-picker';
import { useEffect, useState } from 'react';
import {
  ActivityIndicator, Alert, Image, KeyboardAvoidingView, Modal, Platform, Pressable, ScrollView, StyleSheet, Text, TextInput, View,
} from 'react-native';
import { colors, shadow } from '../../theme';

type Props = {
  visible: boolean;
  username: string;
  submitting: boolean;
  error: string | null;
  onClose: () => void;
  onSubmit: (body: string, tags: string[], image?: SelectedPostImage) => void;
};

export type SelectedPostImage = {
  uri: string;
  fileName?: string | null;
  mimeType?: string | null;
};

export function CreatePostModal({ visible, username, submitting, error, onClose, onSubmit }: Props) {
  const [body, setBody] = useState('');
  const [tagText, setTagText] = useState('');
  const [image, setImage] = useState<SelectedPostImage | undefined>();

  useEffect(() => {
    if (!visible) {
      setBody('');
      setTagText('');
      setImage(undefined);
    }
  }, [visible]);

  const submit = () => {
    const tags = tagText.split(',').map(tag => tag.trim()).filter(Boolean);
    onSubmit(body.trim(), tags, image);
  };

  const pickImage = async () => {
    const permission = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (!permission.granted) {
      Alert.alert('Galeri izni gerekli', 'Fotoğraf ekleyebilmek için Pet’im uygulamasına galeri erişimi vermelisin.');
      return;
    }

    const result = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ['images'],
      allowsEditing: true,
      aspect: [4, 3],
      quality: 0.82,
    });

    if (!result.canceled && result.assets[0]) {
      const asset = result.assets[0];
      setImage({ uri: asset.uri, fileName: asset.fileName, mimeType: asset.mimeType });
    }
  };

  return <Modal visible={visible} animationType="slide" transparent onRequestClose={onClose}>
    <KeyboardAvoidingView style={styles.overlay} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <Pressable style={styles.backdrop} onPress={submitting ? undefined : onClose} />
      <View style={styles.sheet}>
        <View style={styles.handle} />
        <View style={styles.header}>
          <View>
            <Text style={styles.title}>Yeni paylaşım</Text>
            <Text style={styles.subtitle}>@{username} olarak paylaşıyorsun</Text>
          </View>
          <Pressable onPress={onClose} disabled={submitting} style={styles.closeButton} accessibilityLabel="Paylaşım ekranını kapat">
            <Ionicons name="close" size={22} color={colors.primary} />
          </Pressable>
        </View>

        <ScrollView
          style={styles.formScroll}
          contentContainerStyle={styles.formContent}
          keyboardShouldPersistTaps="handled"
          keyboardDismissMode={Platform.OS === 'ios' ? 'interactive' : 'on-drag'}
          automaticallyAdjustKeyboardInsets={Platform.OS === 'ios'}
          showsVerticalScrollIndicator
        >

        {image ? <View style={styles.previewWrap}>
          <Image source={{ uri: image.uri }} style={styles.preview} resizeMode="cover" />
          <Pressable onPress={() => setImage(undefined)} style={styles.removeImage} accessibilityLabel="Seçilen fotoğrafı kaldır">
            <Ionicons name="trash-outline" size={18} color={colors.white} />
          </Pressable>
        </View> : <Pressable onPress={pickImage} style={styles.imagePicker}>
          <View style={styles.imagePickerIcon}><Ionicons name="images-outline" size={24} color={colors.primary} /></View>
          <View><Text style={styles.imagePickerTitle}>Galeriden fotoğraf ekle</Text><Text style={styles.imagePickerHint}>JPG, PNG veya WebP · en fazla 8 MB</Text></View>
          <Ionicons name="chevron-forward" size={19} color={colors.primary} />
        </Pressable>}

        <TextInput
          value={body}
          onChangeText={setBody}
          placeholder="Patinle ilgili bir anını, deneyimini veya bakım notunu paylaş…"
          placeholderTextColor="#94898D"
          multiline
          maxLength={2000}
          autoFocus
          style={styles.bodyInput}
        />
        <Text style={styles.counter}>{body.length}/2000</Text>

        <View style={styles.tagRow}>
          <Ionicons name="pricetags-outline" size={19} color={colors.primary} />
          <TextInput
            value={tagText}
            onChangeText={setTagText}
            placeholder="Etiketler: bakım, kedi (virgülle ayır)"
            placeholderTextColor="#94898D"
            maxLength={180}
            style={styles.tagInput}
          />
        </View>

        {error ? <View style={styles.errorBox}><Ionicons name="alert-circle-outline" size={18} color="#9B463B" /><Text style={styles.errorText}>{error}</Text></View> : null}

        <Pressable
          onPress={submit}
          disabled={submitting || body.trim().length < 2}
          style={({ pressed }) => [styles.submitButton, (submitting || body.trim().length < 2) && styles.disabled, pressed && styles.pressed]}
        >
          {submitting ? <ActivityIndicator color={colors.white} /> : <><Ionicons name="paper-plane" size={18} color={colors.white} /><Text style={styles.submitText}>Paylaş</Text></>}
        </Pressable>
        <Text style={styles.xpNote}>Bu paylaşım hesabına 10 XP kazandırır.</Text>
        </ScrollView>
      </View>
    </KeyboardAvoidingView>
  </Modal>;
}

const styles = StyleSheet.create({
  overlay: { flex: 1, justifyContent: 'flex-end' },
  backdrop: { position: 'absolute', top: 0, right: 0, bottom: 0, left: 0, backgroundColor: '#241B2188' },
  sheet: { maxHeight: '94%', backgroundColor: colors.background, borderTopLeftRadius: 28, borderTopRightRadius: 28, paddingTop: 10, paddingBottom: Platform.OS === 'ios' ? 18 : 12, overflow: 'hidden', ...shadow },
  handle: { width: 42, height: 5, borderRadius: 3, backgroundColor: '#D8CDD1', alignSelf: 'center', marginBottom: 18 },
  header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingHorizontal: 20, paddingBottom: 10 },
  formScroll: { flexShrink: 1 }, formContent: { paddingHorizontal: 20, paddingBottom: 16 },
  title: { color: colors.text, fontFamily: 'serif', fontSize: 25, fontWeight: '700' },
  subtitle: { color: colors.muted, fontSize: 11, marginTop: 3 },
  closeButton: { width: 40, height: 40, borderRadius: 20, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' },
  imagePicker: { minHeight: 72, flexDirection: 'row', alignItems: 'center', gap: 11, marginTop: 18, padding: 12, borderRadius: 18, borderWidth: 1, borderStyle: 'dashed', borderColor: '#CBB8D1', backgroundColor: colors.lilacSoft },
  imagePickerIcon: { width: 42, height: 42, borderRadius: 21, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.card },
  imagePickerTitle: { color: colors.text, fontSize: 12, fontWeight: '900' }, imagePickerHint: { color: colors.muted, fontSize: 9, marginTop: 3 },
  previewWrap: { height: 145, marginTop: 18, borderRadius: 18, overflow: 'hidden', backgroundColor: colors.sageSoft }, preview: { width: '100%', height: '100%' },
  removeImage: { position: 'absolute', top: 10, right: 10, width: 38, height: 38, borderRadius: 19, alignItems: 'center', justifyContent: 'center', backgroundColor: '#6E405FDD' },
  bodyInput: { minHeight: 125, maxHeight: 210, marginTop: 14, padding: 15, color: colors.text, backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border, borderRadius: 18, fontSize: 14, lineHeight: 21, textAlignVertical: 'top' },
  counter: { color: colors.muted, fontSize: 9, textAlign: 'right', marginTop: 5 },
  tagRow: { minHeight: 50, flexDirection: 'row', alignItems: 'center', gap: 9, marginTop: 12, paddingHorizontal: 14, backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border, borderRadius: 16 },
  tagInput: { flex: 1, color: colors.text, fontSize: 12, paddingVertical: 10 },
  errorBox: { flexDirection: 'row', alignItems: 'center', gap: 8, padding: 12, marginTop: 12, borderRadius: 14, backgroundColor: colors.peachSoft },
  errorText: { flex: 1, color: '#8D4339', fontSize: 11, lineHeight: 16 },
  submitButton: { minHeight: 52, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8, marginTop: 16, borderRadius: 18, backgroundColor: colors.primary },
  submitText: { color: colors.white, fontSize: 14, fontWeight: '900' },
  disabled: { opacity: 0.48 },
  pressed: { opacity: 0.8 },
  xpNote: { color: colors.muted, textAlign: 'center', fontSize: 9, marginTop: 9 },
});
