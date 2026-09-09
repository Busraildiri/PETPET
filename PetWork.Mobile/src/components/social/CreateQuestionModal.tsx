import { Ionicons } from '@expo/vector-icons';
import { useEffect, useState } from 'react';
import {
  ActivityIndicator, KeyboardAvoidingView, Modal, Platform, Pressable, ScrollView, StyleSheet, Text, TextInput, View,
} from 'react-native';
import { colors, createThemedStyles, shadow } from '../../theme';

const categories = ['Beslenme', 'Sağlık', 'Davranış', 'Bakım', 'Yas ve Kayıp', 'Diğer'];

type Props = {
  visible: boolean;
  username: string;
  submitting: boolean;
  error: string | null;
  onClose: () => void;
  onSubmit: (title: string, content: string, category: string) => void;
};

export function CreateQuestionModal({ visible, username, submitting, error, onClose, onSubmit }: Props) {
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [category, setCategory] = useState('Bakım');
  const [validationError, setValidationError] = useState<string | null>(null);

  useEffect(() => {
    if (!visible) {
      setTitle('');
      setContent('');
      setCategory('Bakım');
      setValidationError(null);
    }
  }, [visible]);

  const submitQuestion = () => {
    const trimmedTitle = title.trim();
    const trimmedContent = content.trim();

    if (trimmedTitle.length < 5) {
      setValidationError('Soru başlığı en az 5 karakter olmalı.');
      return;
    }

    if (trimmedContent.length < 10) {
      setValidationError('Soru detayı en az 10 karakter olmalı.');
      return;
    }

    setValidationError(null);
    onSubmit(trimmedTitle, trimmedContent, category);
  };

  return <Modal visible={visible} animationType="slide" transparent onRequestClose={onClose}>
    <KeyboardAvoidingView style={styles.overlay} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <Pressable style={styles.backdrop} onPress={submitting ? undefined : onClose} />
      <View style={styles.sheet}>
        <View style={styles.handle} />
        <View style={styles.header}>
          <View style={styles.headerCopy}>
            <Text style={styles.title}>Yeni soru sor</Text>
            <Text style={styles.subtitle}>@{username} olarak topluluğa soruyorsun</Text>
          </View>
          <Pressable onPress={onClose} disabled={submitting} style={styles.closeButton} accessibilityLabel="Soru ekranını kapat">
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
          <Text style={styles.label}>Kategori</Text>
          <View style={styles.categories}>
            {categories.map(item => <Pressable
              key={item}
              onPress={() => setCategory(item)}
              style={[styles.categoryChip, category === item && styles.categoryChipActive]}
            >
              <Text style={[styles.categoryText, category === item && styles.categoryTextActive]}>{item}</Text>
            </Pressable>)}
          </View>

          <Text style={styles.label}>Sorunun başlığı</Text>
          <TextInput
            value={title}
            onChangeText={value => { setTitle(value); setValidationError(null); }}
            placeholder="Örn. Kedimi yeni mamaya nasıl alıştırabilirim?"
            placeholderTextColor="#94898D"
            maxLength={200}
            autoFocus
            style={styles.titleInput}
          />
          <Text style={styles.counter}>{title.length}/200</Text>

          <Text style={styles.label}>Detaylar</Text>
          <TextInput
            value={content}
            onChangeText={value => { setContent(value); setValidationError(null); }}
            placeholder="Yaş, tür ve daha önce denediğin yöntemler gibi yararlı ayrıntıları paylaş…"
            placeholderTextColor="#94898D"
            multiline
            maxLength={4000}
            style={styles.contentInput}
          />
          <Text style={styles.counter}>{content.length}/4000</Text>

          {(error || validationError) ? <View style={styles.errorBox}><Ionicons name="alert-circle-outline" size={18} color="#9B463B" /><Text style={styles.errorText}>{error || validationError}</Text></View> : null}

          <Pressable
            onPress={submitQuestion}
            disabled={submitting}
            style={({ pressed }) => [styles.submitButton, submitting && styles.disabled, pressed && styles.pressed]}
          >
            {submitting ? <ActivityIndicator color={colors.white} /> : <><Ionicons name="send" size={18} color={colors.white} /><Text style={styles.submitText}>Soruyu yayınla</Text></>}
          </Pressable>
          <Text style={styles.xpNote}>Sorun web ve mobil toplulukta yayınlanır, hesabına 10 XP kazandırır.</Text>
        </ScrollView>
      </View>
    </KeyboardAvoidingView>
  </Modal>;
}

const styles = createThemedStyles(() => ({
  overlay: { flex: 1, justifyContent: 'flex-end' },
  backdrop: { position: 'absolute', top: 0, right: 0, bottom: 0, left: 0, backgroundColor: '#241B2188' },
  sheet: { maxHeight: '94%', backgroundColor: colors.background, borderTopLeftRadius: 28, borderTopRightRadius: 28, paddingTop: 10, paddingBottom: Platform.OS === 'ios' ? 18 : 12, overflow: 'hidden', ...shadow },
  handle: { width: 42, height: 5, borderRadius: 3, backgroundColor: '#D8CDD1', alignSelf: 'center', marginBottom: 18 },
  header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingHorizontal: 20, paddingBottom: 10 },
  headerCopy: { flex: 1, paddingRight: 12 },
  title: { color: colors.text, fontFamily: 'serif', fontSize: 25, fontWeight: '700' },
  subtitle: { color: colors.muted, fontSize: 11, marginTop: 3 },
  closeButton: { width: 40, height: 40, borderRadius: 20, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' },
  formScroll: { flexShrink: 1 },
  formContent: { paddingHorizontal: 20, paddingBottom: 18 },
  label: { color: colors.text, fontSize: 12, fontWeight: '900', marginTop: 14, marginBottom: 8 },
  categories: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  categoryChip: { paddingHorizontal: 12, paddingVertical: 8, borderRadius: 16, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card },
  categoryChipActive: { borderColor: colors.primary, backgroundColor: colors.lilacSoft },
  categoryText: { color: colors.muted, fontSize: 10, fontWeight: '800' },
  categoryTextActive: { color: colors.primary },
  titleInput: { minHeight: 52, paddingHorizontal: 14, color: colors.text, backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border, borderRadius: 16, fontSize: 13 },
  contentInput: { minHeight: 145, maxHeight: 240, padding: 15, color: colors.text, backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border, borderRadius: 18, fontSize: 13, lineHeight: 20, textAlignVertical: 'top' },
  counter: { color: colors.muted, fontSize: 9, textAlign: 'right', marginTop: 5 },
  errorBox: { flexDirection: 'row', alignItems: 'center', gap: 8, padding: 12, marginTop: 12, borderRadius: 14, backgroundColor: colors.peachSoft },
  errorText: { flex: 1, color: '#8D4339', fontSize: 11, lineHeight: 16 },
  submitButton: { minHeight: 52, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8, marginTop: 17, borderRadius: 18, backgroundColor: colors.primary },
  submitText: { color: colors.white, fontSize: 14, fontWeight: '900' },
  disabled: { opacity: 0.48 },
  pressed: { opacity: 0.8 },
  xpNote: { color: colors.muted, textAlign: 'center', fontSize: 9, marginTop: 9 },
}));
