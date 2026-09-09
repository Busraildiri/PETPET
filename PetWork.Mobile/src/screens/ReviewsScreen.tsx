import { Ionicons } from '@expo/vector-icons';
import * as ImagePicker from 'expo-image-picker';
import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator, Alert, Image, Keyboard, KeyboardAvoidingView, Platform, Pressable,
  RefreshControl, ScrollView, StyleSheet, Text, TextInput, View,
} from 'react-native';
import { createProductReview, getProductReviews, mediaUrl, ProductReview, reportProductReview } from '../api';
import { colors, createThemedStyles, shadow } from '../theme';

type Props = { token: string | null; onLogin: () => void; onBack: () => void };

export function ReviewsScreen({ token, onLogin, onBack }: Props) {
  const [items, setItems] = useState<ProductReview[]>([]); const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false); const [error, setError] = useState<string | null>(null); const [adding, setAdding] = useState(false);
  const load = useCallback(async (refresh = false) => { refresh ? setRefreshing(true) : setLoading(true); setError(null); try { setItems(await getProductReviews()); } catch (reason) { setError(reason instanceof Error ? reason.message : 'Deneyimler yüklenemedi.'); } finally { setLoading(false); setRefreshing(false); } }, []);
  useEffect(() => { void load(); }, [load]);
  const requireLogin = () => { if (token) return true; Alert.alert('Giriş gerekli', 'Deneyim paylaşmak veya bildirim yapmak için giriş yapmalısın.', [{ text: 'Vazgeç' }, { text: 'Giriş yap', onPress: onLogin }]); return false; };
  const report = (item: ProductReview) => { if (!requireLogin() || !token) return; Alert.alert('İçeriği bildir', 'Bu deneyimi uygunsuz veya yanıltıcı içerik olarak bildirmek istiyor musun?', [{ text: 'Vazgeç' }, { text: 'Bildir', style: 'destructive', onPress: async () => { try { Alert.alert('Teşekkürler', await reportProductReview(token, item.id, 'Uygunsuz veya yanıltıcı içerik')); } catch (reason) { Alert.alert('Bildirim gönderilemedi', reason instanceof Error ? reason.message : 'Lütfen yeniden dene.'); } } }]); };
  return <KeyboardAvoidingView style={styles.page} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
    <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={styles.content} refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load(true)} tintColor={colors.primary} />}>
      <Header onBack={onBack} />
      <View style={styles.notice}><Ionicons name="nutrition-outline" size={24} color="#8A6515" /><Text style={styles.noticeText}>Puanlar kullanıcı deneyimidir; veteriner önerisi veya ürün garantisi değildir.</Text></View>
      <Pressable style={styles.primaryButton} onPress={() => { if (requireLogin()) setAdding(value => !value); }}><Ionicons name={adding ? 'close' : 'add-circle-outline'} size={20} color="#fff" /><Text style={styles.primaryText}>{adding ? 'Formu kapat' : 'Mama deneyimi ekle'}</Text></Pressable>
      {adding && token ? <ReviewForm token={token} onCreated={item => { setItems(current => [item, ...current]); setAdding(false); }} /> : null}
      <Text style={styles.sectionTitle}>Topluluk deneyimleri</Text>
      {loading ? <State loading text="Deneyimler yükleniyor…" /> : null}
      {!loading && error ? <Pressable onPress={() => void load()}><State text={`${error}\nTekrar denemek için dokun.`} /></Pressable> : null}
      {!loading && !error && items.length === 0 ? <State text="İlk mama deneyimini sen paylaşabilirsin." /> : null}
      {items.map(item => <View key={item.id} style={styles.card}>{item.imagePath ? <Image source={{ uri: mediaUrl(item.imagePath) }} style={styles.image} /> : <View style={styles.package}><Ionicons name="nutrition" size={38} color="#8A6515" /><Text style={styles.packageText}>MAMA</Text></View>}
        <View style={styles.cardBody}><View style={styles.row}><View style={styles.flex}><Text style={styles.product}>{item.productName}</Text><Text style={styles.meta}>{item.brand} · {item.petType}</Text></View><Text style={styles.average}>{item.averageScore.toFixed(1).replace('.', ',')} ★</Text></View>
          <Text style={styles.user}>@{item.username} · {new Date(item.createdAt).toLocaleDateString('tr-TR')}</Text>
          <Text style={styles.experience}>{item.experience}</Text>
          <View style={styles.scores}><Score label="Lezzet" value={item.tasteScore} /><Score label="İçerik" value={item.ingredientScore} /><Score label="Sindirim" value={item.digestionScore} /><Score label="Fiyat/Değer" value={item.valueScore} /></View>
          <Pressable onPress={() => report(item)}><Text style={styles.report}>İçeriği bildir</Text></Pressable>
        </View>
      </View>)}
    </ScrollView>
  </KeyboardAvoidingView>;
}

function ReviewForm({ token, onCreated }: { token: string; onCreated: (item: ProductReview) => void }) {
  const [photo, setPhoto] = useState<ImagePicker.ImagePickerAsset | null>(null); const [busy, setBusy] = useState(false);
  const [brand, setBrand] = useState(''); const [productName, setProductName] = useState(''); const [petType, setPetType] = useState(''); const [experience, setExperience] = useState('');
  const [tasteScore, setTaste] = useState(0); const [ingredientScore, setIngredient] = useState(0); const [digestionScore, setDigestion] = useState(0); const [valueScore, setValue] = useState(0);
  const pick = async () => { const permission = await ImagePicker.requestMediaLibraryPermissionsAsync(); if (!permission.granted) { Alert.alert('Fotoğraf izni gerekli', 'Paket fotoğrafı seçebilmek için galeri izni ver.'); return; } const result = await ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images'], allowsEditing: true, aspect: [4, 3], quality: 0.85 }); if (!result.canceled) setPhoto(result.assets[0]); };
  const submit = async () => {
    Keyboard.dismiss();
    if (brand.trim().length < 2 || productName.trim().length < 2 || petType.trim().length < 2 || experience.trim().length < 10) { Alert.alert('Eksik bilgi', 'Marka, ürün, pati türü ve en az 10 karakterlik deneyimini yaz.'); return; }
    if ([tasteScore, ingredientScore, digestionScore, valueScore].some(score => score < 1)) { Alert.alert('Puanlar eksik', 'Dört ölçütün tamamına 1–5 arasında puan ver.'); return; }
    setBusy(true); try { onCreated(await createProductReview(token, { brand, productName, petType, experience, tasteScore, ingredientScore, digestionScore, valueScore, image: photo ? { uri: photo.uri, mimeType: photo.mimeType } : undefined })); Alert.alert('Deneyim kaydedildi', 'Değerlendirmen topluluk akışına eklendi.'); }
    catch (reason) { Alert.alert('Deneyim kaydedilemedi', reason instanceof Error ? reason.message : 'Lütfen yeniden dene.'); } finally { setBusy(false); }
  };
  return <View style={styles.form}><Text style={styles.formTitle}>Yeni deneyim</Text><Pressable style={styles.photoPicker} onPress={() => void pick()}>{photo ? <Image source={{ uri: photo.uri }} style={styles.preview} /> : <><Ionicons name="camera-outline" size={26} color={colors.primary} /><Text style={styles.link}>Paket fotoğrafı ekle (isteğe bağlı)</Text></>}</Pressable>
    <Field value={brand} onChange={setBrand} placeholder="Marka *" /><Field value={productName} onChange={setProductName} placeholder="Ürün adı *" /><Field value={petType} onChange={setPetType} placeholder="Evcil hayvan türü *" /><Field value={experience} onChange={setExperience} placeholder="Deneyimini anlat *" multiline />
    <Rating label="Lezzet" value={tasteScore} onChange={setTaste} /><Rating label="İçerik" value={ingredientScore} onChange={setIngredient} /><Rating label="Sindirim" value={digestionScore} onChange={setDigestion} /><Rating label="Fiyat / Değer" value={valueScore} onChange={setValue} />
    <Pressable disabled={busy} style={[styles.primaryButton, busy && styles.disabled]} onPress={() => void submit()}>{busy ? <ActivityIndicator color="#fff" /> : <Text style={styles.primaryText}>Deneyimi kaydet</Text>}</Pressable>
  </View>;
}

function Rating({ label, value, onChange }: { label: string; value: number; onChange: (value: number) => void }) { return <View style={styles.ratingRow}><Text style={styles.ratingLabel}>{label}</Text><View style={styles.stars}>{[1, 2, 3, 4, 5].map(score => <Pressable key={score} onPress={() => onChange(score)} hitSlop={5}><Ionicons name={score <= value ? 'star' : 'star-outline'} size={27} color="#D39B25" /></Pressable>)}</View><Text style={styles.ratingNumber}>{value || '–'}</Text></View>; }
function Score({ label, value }: { label: string; value: number }) { return <View style={styles.score}><Text style={styles.scoreValue}>{value}</Text><Text style={styles.scoreLabel}>{label}</Text></View>; }
function Field({ value, onChange, placeholder, multiline }: { value: string; onChange: (value: string) => void; placeholder: string; multiline?: boolean }) { return <TextInput value={value} onChangeText={onChange} placeholder={placeholder} placeholderTextColor="#958991" multiline={multiline} maxLength={multiline ? 1500 : 150} style={[styles.input, multiline && styles.multiline]} />; }
function State({ text, loading }: { text: string; loading?: boolean }) { return <View style={styles.state}>{loading ? <ActivityIndicator color={colors.primary} /> : <Ionicons name="chatbox-ellipses-outline" size={30} color={colors.primary} />}<Text style={styles.experience}>{text}</Text></View>; }
function Header({ onBack }: { onBack: () => void }) { return <View style={styles.header}><Pressable style={styles.back} onPress={onBack}><Ionicons name="arrow-back" size={30} color={colors.primary} /></Pressable><View><Text style={styles.title}>Pati Denedi</Text><Text style={styles.subtitle}>Gerçek mama deneyimleri</Text></View></View>; }

const styles = createThemedStyles(() => ({
  page: { flex: 1, backgroundColor: '#FFF9F3' }, content: { padding: 24, paddingTop: 62, paddingBottom: 130 }, header: { flexDirection: 'row', alignItems: 'center', gap: 16, marginBottom: 24 },
  back: { width: 54, height: 54, borderRadius: 27, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' }, title: { fontSize: 37, fontWeight: '800', color: '#30262B' }, subtitle: { fontSize: 16, color: '#776D72' },
  notice: { flexDirection: 'row', gap: 12, padding: 16, borderRadius: 19, backgroundColor: colors.yellowSoft }, noticeText: { color: '#755B20', flex: 1, lineHeight: 21 },
  primaryButton: { minHeight: 50, borderRadius: 16, backgroundColor: colors.primary, flexDirection: 'row', gap: 8, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 16, marginTop: 14 }, primaryText: { color: '#fff', fontWeight: '800', fontSize: 16 }, disabled: { opacity: 0.5 },
  sectionTitle: { fontSize: 28, fontWeight: '800', color: '#30262B', marginTop: 28, marginBottom: 12 }, state: { alignItems: 'center', gap: 10, padding: 28, borderRadius: 20, backgroundColor: '#fff' },
  card: { borderRadius: 23, backgroundColor: '#fff', overflow: 'hidden', marginBottom: 20, ...shadow }, image: { height: 210, width: '100%' }, package: { height: 130, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.yellowSoft }, packageText: { color: '#8A6515', fontWeight: '900', marginTop: 5 },
  cardBody: { padding: 18 }, row: { flexDirection: 'row', justifyContent: 'space-between', gap: 12, alignItems: 'flex-start' }, flex: { flex: 1 }, product: { fontSize: 22, fontWeight: '800', color: '#30262B' }, meta: { color: '#756A70', fontSize: 16, marginTop: 4 }, average: { color: '#8A6515', backgroundColor: colors.yellowSoft, paddingVertical: 7, paddingHorizontal: 10, borderRadius: 13, overflow: 'hidden', fontWeight: '900' },
  user: { color: '#91868C', marginTop: 8 }, experience: { color: '#675D62', lineHeight: 22, marginTop: 10 }, scores: { flexDirection: 'row', marginTop: 16, borderTopWidth: 1, borderColor: '#EEE5E0', paddingTop: 14 }, score: { flex: 1, alignItems: 'center' }, scoreValue: { color: colors.primary, fontSize: 20, fontWeight: '900' }, scoreLabel: { fontSize: 10, color: '#83777D', textAlign: 'center', marginTop: 3 }, report: { color: '#A44E45', fontWeight: '800', textAlign: 'right', paddingTop: 18 },
  form: { padding: 16, borderRadius: 22, backgroundColor: '#fff', marginTop: 16, ...shadow }, formTitle: { fontSize: 21, fontWeight: '800', color: '#30262B', marginBottom: 10 }, photoPicker: { minHeight: 88, borderWidth: 1.5, borderStyle: 'dashed', borderColor: '#C9AED3', borderRadius: 16, alignItems: 'center', justifyContent: 'center', overflow: 'hidden' }, preview: { width: '100%', height: 170 }, link: { color: colors.primary, fontWeight: '800', marginTop: 5 },
  input: { minHeight: 50, borderWidth: 1, borderColor: '#E1D8D3', backgroundColor: '#FFFCF9', borderRadius: 15, paddingHorizontal: 14, fontSize: 16, marginTop: 10 }, multiline: { minHeight: 105, paddingTop: 13, textAlignVertical: 'top' },
  ratingRow: { flexDirection: 'row', alignItems: 'center', marginTop: 14 }, ratingLabel: { width: 92, fontWeight: '700', color: '#4D4147' }, stars: { flexDirection: 'row', flex: 1, justifyContent: 'space-between' }, ratingNumber: { width: 22, textAlign: 'right', color: colors.primary, fontWeight: '900' },
}));
