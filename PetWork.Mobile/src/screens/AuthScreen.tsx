import { Ionicons } from '@expo/vector-icons';
import { useState } from 'react';
import {
  ActivityIndicator, Alert, KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Text, TextInput, View,
  useWindowDimensions,
} from 'react-native';
import { StatusBar } from 'expo-status-bar';
import * as SecureStore from 'expo-secure-store';
import { loginUser, registerUser, type AuthResponse } from '../api';
import { colors, shadow } from '../theme';

export type AuthMode = 'login' | 'register';

export function AuthScreen({ initialMode, onBack, onAuthenticated }: { initialMode: AuthMode; onBack: () => void; onAuthenticated: (session: AuthResponse) => void }) {
  const { width, height } = useWindowDimensions();
  const [mode, setMode] = useState<AuthMode>(initialMode);
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [remember, setRemember] = useState(true);
  const [accepted, setAccepted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const isLogin = mode === 'login';
  const isPhone = width < 600;
  const isShort = height < 760;

  const switchMode = (next: AuthMode) => {
    setMode(next);
    setPassword('');
    setConfirmPassword('');
    setFormError(null);
  };

  const submit = async () => {
    setFormError(null);
    if (!email.trim() || !password) {
      setFormError('E-posta ve şifre alanlarını doldurmalısın.');
      return;
    }
    if (!isLogin && !name.trim()) {
      setFormError('Kullanıcı adı alanını doldurmalısın.');
      return;
    }
    if (!isLogin && password !== confirmPassword) {
      setFormError('Şifre ve şifre tekrar alanları eşleşmiyor.');
      return;
    }
    if (!isLogin && !accepted) {
      setFormError('Üyelik koşullarını ve gizlilik politikasını kabul etmelisin.');
      return;
    }

    setSubmitting(true);
    try {
      const result = isLogin
        ? await loginUser({ emailOrUsername: email.trim(), password, rememberMe: remember })
        : await registerUser({
          username: name.trim(), email: email.trim(), password, confirmPassword, acceptTerms: accepted,
        });
      await SecureStore.setItemAsync('petim.session', JSON.stringify({
        token: result.token,
        userId: result.userId,
        username: result.username,
        expiresAt: result.expiresAt,
      }));
      Alert.alert(isLogin ? 'Hoş geldin!' : 'Kaydın tamamlandı!', result.message, [{ text: 'Devam Et', onPress: () => onAuthenticated(result) }]);
    } catch (reason) {
      setFormError(reason instanceof Error ? reason.message : `${isLogin ? 'Giriş' : 'Kayıt'} işlemi tamamlanamadı.`);
    } finally {
      setSubmitting(false);
    }
  };

  return <KeyboardAvoidingView style={styles.page} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
    <StatusBar style="dark" />
    <ScrollView contentContainerStyle={[styles.scroll, isPhone && styles.scrollPhone]} keyboardShouldPersistTaps="handled" showsVerticalScrollIndicator={false} contentInsetAdjustmentBehavior="never">
      <View style={[
        styles.card,
        !isLogin && styles.registerCard,
        isPhone && styles.cardPhone,
        isPhone && { minHeight: Math.max(height, isLogin ? 700 : 790) },
      ]}>
        <View style={styles.cornerWash} /><View style={styles.ribbonTop} /><View style={styles.ribbonSide} />
        <View style={styles.cardTopRow}>
          <Pressable onPress={onBack} accessibilityRole="button" accessibilityLabel="Ana sayfaya dön" style={styles.backLink}>
            <Ionicons name="arrow-back" size={20} color={colors.primary} />
          </Pressable>
          <Pressable onPress={onBack} accessibilityRole="button" accessibilityLabel="Menüyü kapat" style={styles.menuButton}>
            <Ionicons name="menu" size={27} color={colors.text} />
          </Pressable>
        </View>

        <View style={[styles.formArea, !isLogin && styles.formAreaRegister, isShort && styles.formAreaShort, !isLogin && isShort && styles.formAreaRegisterShort]}>
        <Text style={styles.title}>{isLogin ? 'Giriş Yap' : 'Hesap Oluştur'}</Text>

        {!isLogin ? <AuthInput icon="person-outline" value={name} onChangeText={setName} placeholder="Ad veya kullanıcı adı" autoCapitalize="words" /> : null}
        <AuthInput icon="mail-outline" value={email} onChangeText={setEmail} placeholder={isLogin ? 'E-posta veya kullanıcı adı' : 'E-posta adresi'} keyboardType="email-address" />
        <AuthInput icon="lock-closed-outline" value={password} onChangeText={setPassword} placeholder="Şifre" secureTextEntry={!showPassword}
          right={<Pressable onPress={() => setShowPassword(value => !value)} accessibilityLabel={showPassword ? 'Şifreyi gizle' : 'Şifreyi göster'} hitSlop={10}>
            <Ionicons name={showPassword ? 'eye-off-outline' : 'eye-outline'} size={20} color={colors.muted} />
          </Pressable>} />
        {!isLogin ? <AuthInput icon="shield-checkmark-outline" value={confirmPassword} onChangeText={setConfirmPassword} placeholder="Şifre tekrar" secureTextEntry={!showPassword} /> : null}

        {isLogin ? <View style={styles.optionsRow}>
          <CheckRow checked={remember} onPress={() => setRemember(!remember)} label="Beni hatırla" />
          <Pressable onPress={() => Alert.alert('Şifremi unuttum', 'Şifre yenileme bağlantısı e-posta adresine gönderilecek.')}><Text style={styles.linkSmall}>Şifremi unuttum</Text></Pressable>
        </View> : <View style={styles.termsBox}><CheckRow checked={accepted} onPress={() => setAccepted(!accepted)} label="Üyelik koşullarını ve gizlilik politikasını kabul ediyorum." /></View>}

        {formError ? <View style={styles.errorBox}><Ionicons name="alert-circle-outline" size={17} color="#9B463B" /><Text style={styles.errorText}>{formError}</Text></View> : null}

        <Pressable onPress={submit} disabled={submitting} accessibilityRole="button" accessibilityState={{ disabled: submitting }} style={({ pressed }) => [styles.submitButton, submitting && styles.submitDisabled, pressed && styles.pressed]}>
          {submitting ? <ActivityIndicator color={colors.white} /> : <><Text style={styles.submitText}>{isLogin ? 'Giriş Yap' : 'Üye Ol'}</Text><Ionicons name="arrow-forward" size={19} color={colors.white} /></>}
        </Pressable>

        <View style={styles.dividerRow}><View style={styles.divider} /><Text style={styles.dividerText}>veya</Text><View style={styles.divider} /></View>
        <Pressable onPress={() => Alert.alert('Yakında', 'Sosyal hesapla giriş daha sonra eklenecek.')} style={styles.secondaryButton}>
          <Ionicons name="logo-google" size={18} color={colors.primary} /><Text style={styles.secondaryText}>Google ile devam et</Text>
        </Pressable>

        <View style={styles.switchRow}><Text style={styles.switchText}>{isLogin ? 'Henüz hesabın yok mu?' : 'Zaten hesabın var mı?'}</Text>
          <Pressable onPress={() => switchMode(isLogin ? 'register' : 'login')}><Text style={styles.switchLink}>{isLogin ? 'Üye Ol' : 'Giriş Yap'}</Text></Pressable>
        </View>
        </View>
      </View>
    </ScrollView>
  </KeyboardAvoidingView>;
}

function AuthInput({ icon, right, ...props }: {
  icon: keyof typeof Ionicons.glyphMap;
  right?: React.ReactNode;
  value: string;
  onChangeText: (value: string) => void;
  placeholder: string;
  secureTextEntry?: boolean;
  keyboardType?: 'default' | 'email-address';
  autoCapitalize?: 'none' | 'words';
}) {
  return <View style={styles.inputWrap}><Ionicons name={icon} size={19} color={colors.primary} />
    <TextInput {...props} style={styles.input} placeholderTextColor="#94898D" autoCapitalize={props.autoCapitalize ?? 'none'} autoCorrect={false} />{right}
  </View>;
}

function CheckRow({ checked, onPress, label }: { checked: boolean; onPress: () => void; label: string }) {
  return <Pressable onPress={onPress} accessibilityRole="checkbox" accessibilityState={{ checked }} style={styles.checkRow}>
    <View style={[styles.checkbox, checked && styles.checkboxChecked]}>{checked ? <Ionicons name="checkmark" size={14} color={colors.white} /> : null}</View>
    <Text style={styles.checkLabel}>{label}</Text>
  </Pressable>;
}

const serif = Platform.select({ ios: 'Georgia', android: 'serif', default: 'serif' });

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: colors.card }, scroll: { flexGrow: 1, justifyContent: 'center', paddingVertical: Platform.OS === 'ios' ? 30 : 18, paddingHorizontal: 24, backgroundColor: '#F2F0F1' }, scrollPhone: { justifyContent: 'flex-start', paddingVertical: 0, paddingHorizontal: 0, backgroundColor: colors.card },
  card: { width: '100%', maxWidth: 390, minHeight: 650, alignSelf: 'center', backgroundColor: colors.card, borderRadius: 24, paddingHorizontal: 34, paddingVertical: 24, overflow: 'hidden', ...shadow }, registerCard: { minHeight: 720 },
  cardPhone: { maxWidth: '100%', borderRadius: 0, paddingTop: Platform.OS === 'ios' ? 52 : 28, paddingBottom: 32, shadowOpacity: 0, elevation: 0 },
  cornerWash: { position: 'absolute', width: 245, height: 175, borderBottomRightRadius: 120, backgroundColor: colors.yellowSoft, opacity: 0.58, top: 0, left: 0 },
  ribbonTop: { position: 'absolute', width: 230, height: 28, borderRadius: 18, backgroundColor: colors.peach, top: 30, left: 95, transform: [{ rotate: '-24deg' }] },
  ribbonSide: { position: 'absolute', width: 190, height: 28, borderRadius: 18, backgroundColor: colors.peach, top: 119, left: -65, transform: [{ rotate: '-59deg' }] },
  cardTopRow: { zIndex: 2, flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }, backLink: { width: 38, height: 38, borderRadius: 19, backgroundColor: '#FFFCF8CC', alignItems: 'center', justifyContent: 'center' }, menuButton: { width: 38, height: 38, alignItems: 'center', justifyContent: 'center' },
  formArea: { marginTop: 135 }, formAreaRegister: { marginTop: 82 }, formAreaShort: { marginTop: 98 }, formAreaRegisterShort: { marginTop: 48 }, title: { color: colors.text, fontSize: 28, fontWeight: '900', marginBottom: 18 },
  inputWrap: { minHeight: 53, flexDirection: 'row', alignItems: 'center', gap: 10, borderBottomWidth: 1, borderBottomColor: '#D8C9D3', marginTop: 7 }, input: { flex: 1, color: colors.text, fontSize: 13, paddingVertical: 14 },
  optionsRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8, marginTop: 13 }, checkRow: { flexDirection: 'row', alignItems: 'center', gap: 7, flex: 1 },
  checkbox: { width: 17, height: 17, borderRadius: 3, borderWidth: 1, borderColor: '#B7A7B1', backgroundColor: colors.yellowSoft, alignItems: 'center', justifyContent: 'center' }, checkboxChecked: { backgroundColor: colors.primary, borderColor: colors.primary },
  checkLabel: { flex: 1, color: colors.text, fontSize: 9, lineHeight: 14 }, linkSmall: { color: colors.text, fontSize: 9, fontWeight: '600' }, termsBox: { marginTop: 14, backgroundColor: colors.sageSoft, borderRadius: 12, padding: 10 },
  errorBox: { flexDirection: 'row', alignItems: 'center', gap: 7, backgroundColor: colors.peachSoft, borderRadius: 10, padding: 10, marginTop: 10 }, errorText: { flex: 1, color: '#823D35', fontSize: 10, lineHeight: 14 },
  submitButton: { minHeight: 45, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 7, backgroundColor: colors.primary, borderRadius: 7, marginTop: 19 }, submitDisabled: { opacity: 0.6 }, submitText: { color: colors.white, fontSize: 13, fontWeight: '900', textTransform: 'uppercase' }, pressed: { opacity: 0.8, transform: [{ scale: 0.99 }] },
  dividerRow: { flexDirection: 'row', alignItems: 'center', gap: 10, marginVertical: 15 }, divider: { height: 1, flex: 1, backgroundColor: colors.border }, dividerText: { color: colors.muted, fontSize: 8 },
  secondaryButton: { minHeight: 43, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8, borderWidth: 1, borderColor: '#CAB8C5', borderRadius: 7, backgroundColor: '#FFFFFFB8' }, secondaryText: { color: colors.primary, fontSize: 11, fontWeight: '800' },
  switchRow: { flexDirection: 'row', justifyContent: 'center', alignItems: 'center', flexWrap: 'wrap', gap: 4, marginTop: 17 }, switchText: { color: colors.text, fontSize: 9 }, switchLink: { color: colors.peach, fontSize: 9, fontWeight: '900', textDecorationLine: 'underline' },
});
