import { Ionicons } from '@expo/vector-icons';
import { Pressable, StyleSheet, Switch, Text, View } from 'react-native';
import type { MobileContactSettings } from '../api';
import { colors } from '../theme';

export type ListingContactSelection = {
  allowInAppMessages: boolean;
  sharePhone: boolean;
  shareEmail: boolean;
};

type PickerProps = {
  context: 'adoption' | 'lost';
  settings: MobileContactSettings | null;
  value: ListingContactSelection;
  onChange: (value: ListingContactSelection) => void;
};

export function ListingContactMethodPicker({ context, settings, value, onChange }: PickerProps) {
  const permission = context === 'adoption' ? settings?.allowAdoptionSharing : settings?.allowLostPetSharing;
  const phoneAvailable = permission === true && Boolean(settings?.phone);
  const emailAvailable = permission === true && Boolean(settings?.email);
  const update = (key: keyof ListingContactSelection, selected: boolean) => onChange({ ...value, [key]: selected });

  return <View style={styles.picker}>
    <Text style={styles.title}>İletişim yöntemi</Text>
    <Text style={styles.helper}>Bu seçimler yalnızca bu ilan için geçerlidir.</Text>
    <MethodRow icon="chatbubble-ellipses-outline" title="Uygulama içinden mesaj" subtitle={context === 'adoption' ? 'Başvuruları uygulamada al' : 'Görülme bildirimlerini uygulamada al'} value={value.allowInAppMessages} onChange={selected => update('allowInAppMessages', selected)} />
    <MethodRow icon="call-outline" title="Telefon" subtitle={phoneAvailable ? settings!.phone! : 'Ayarlar > İletişim bilgileri bölümünden etkinleştir'} value={value.sharePhone} disabled={!phoneAvailable} onChange={selected => update('sharePhone', selected)} />
    <MethodRow icon="mail-outline" title="E-posta" subtitle={emailAvailable ? settings!.email! : 'Ayarlar > İletişim bilgileri bölümünden etkinleştir'} value={value.shareEmail} disabled={!emailAvailable} onChange={selected => update('shareEmail', selected)} last />
  </View>;
}

function MethodRow({ icon, title, subtitle, value, disabled, onChange, last }: {
  icon: keyof typeof Ionicons.glyphMap; title: string; subtitle: string; value: boolean;
  disabled?: boolean; onChange: (value: boolean) => void; last?: boolean;
}) {
  return <Pressable disabled={disabled} onPress={() => onChange(!value)} style={[styles.row, !last && styles.separator, disabled && styles.disabled]}>
    <View style={styles.icon}><Ionicons name={icon} size={20} color={colors.primary} /></View>
    <View style={styles.copy}><Text style={styles.rowTitle}>{title}</Text><Text style={styles.subtitle}>{subtitle}</Text></View>
    <Switch value={value} disabled={disabled} onValueChange={onChange} trackColor={{ false: '#C8C1C5', true: '#A9C8AC' }} thumbColor="#FFFFFF" />
  </Pressable>;
}

export function ListingContactDetails({ allowInAppMessages, phone, email, inAppText }: {
  allowInAppMessages: boolean; phone?: string | null; email?: string | null; inAppText: string;
}) {
  if (!allowInAppMessages && !phone && !email) return null;
  return <View style={styles.details}>
    <Text style={styles.title}>İletişim yöntemleri</Text>
    {allowInAppMessages ? <ContactLine icon="chatbubble-ellipses-outline" text={inAppText} /> : null}
    {phone ? <ContactLine icon="call-outline" label="Telefon" text={phone} selectable /> : null}
    {email ? <ContactLine icon="mail-outline" label="E-posta" text={email} selectable /> : null}
  </View>;
}

function ContactLine({ icon, label, text, selectable }: {
  icon: keyof typeof Ionicons.glyphMap; label?: string; text: string; selectable?: boolean;
}) {
  return <View style={styles.contactLine}><Ionicons name={icon} size={19} color="#4E7458" /><Text selectable={selectable} style={styles.contactText}>{label ? `${label}: ` : ''}{text}</Text></View>;
}

const styles = StyleSheet.create({
  picker: { marginTop: 14, borderWidth: 1, borderColor: '#E1D8D3', borderRadius: 18, paddingHorizontal: 14, backgroundColor: '#FFFCF9' },
  title: { color: '#3D3036', fontWeight: '800', fontSize: 16, marginTop: 14 },
  helper: { color: '#7D7278', fontSize: 13, lineHeight: 18, marginTop: 3, marginBottom: 4 },
  row: { minHeight: 68, flexDirection: 'row', alignItems: 'center', gap: 10, paddingVertical: 10 },
  separator: { borderBottomWidth: StyleSheet.hairlineWidth, borderBottomColor: '#E1D8D3' },
  icon: { width: 36, height: 36, borderRadius: 18, backgroundColor: '#F1E5F5', alignItems: 'center', justifyContent: 'center' },
  copy: { flex: 1 }, rowTitle: { color: '#3D3036', fontWeight: '800', fontSize: 15 },
  subtitle: { color: '#7D7278', fontSize: 12, lineHeight: 17, marginTop: 2 }, disabled: { opacity: 0.5 },
  details: { marginTop: 16, padding: 14, borderRadius: 16, backgroundColor: '#EAF3E8', borderWidth: 1, borderColor: '#CFE0CC' },
  contactLine: { flexDirection: 'row', alignItems: 'center', gap: 9, marginTop: 9 },
  contactText: { flex: 1, color: '#415D48', fontSize: 14, lineHeight: 20, fontWeight: '600' },
});
