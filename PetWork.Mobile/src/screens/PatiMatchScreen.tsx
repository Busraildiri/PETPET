import { Ionicons } from '@expo/vector-icons';
import { StatusBar } from 'expo-status-bar';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, Alert, Animated, Image, PanResponder, Platform, Pressable, RefreshControl, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { deactivatePatiMatch, decidePatiMatch, enrollPatiMatch, getLocationSuggestions, getPatiMatchCandidates, getPatiMatches, getPatiMatchOverview, mediaUrl, type LocationSuggestion, type PatiMatchCandidate, type PatiMatchMyPet, type PatiMatchOverview } from '../api';
import { colors, createThemedStyles, shadow } from '../theme';
import { PatiMatchChatScreen } from './PatiMatchChatScreen';

type Props = { token: string | null; username: string | null; onLogin: () => void; onOpenPets: () => void; onSessionExpired: () => void; onChatStateChange: (open: boolean) => void; initialTargetPetId?: number | null };
const purposeLabels = { friendship: 'Oyun arkadaşı', mate: 'Eş arıyor' } as const;
const petTypes = ['Kedi', 'Köpek', 'Kuş', 'Tavşan', 'Balık', 'Diğer'];

export function PatiMatchScreen({ token, username, onLogin, onOpenPets, onSessionExpired, onChatStateChange, initialTargetPetId }: Props) {
  const [overview, setOverview] = useState<PatiMatchOverview | null>(null);
  const [selectedPetId, setSelectedPetId] = useState<number | null>(null);
  const [candidates, setCandidates] = useState<PatiMatchCandidate[]>([]);
  const [matches, setMatches] = useState<PatiMatchCandidate[]>([]);
  const [loading, setLoading] = useState(Boolean(token));
  const [refreshing, setRefreshing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [city, setCity] = useState('');
  const [district, setDistrict] = useState('');
  const [selectedCityId, setSelectedCityId] = useState<string | null>(null);
  const [selectedDistrictId, setSelectedDistrictId] = useState<string | null>(null);
  const [purpose, setPurpose] = useState<'friendship' | 'mate'>('friendship');
  const [preferredTypes, setPreferredTypes] = useState<string[]>([]);
  const [accepted, setAccepted] = useState(false);
  const [editingPetId, setEditingPetId] = useState<number | null>(null);
  const [chatMatch, setChatMatch] = useState<PatiMatchCandidate | null>(null);
  const [openedInitialTargetId, setOpenedInitialTargetId] = useState<number | null>(null);

  const activePets = useMemo(() => overview?.pets.filter(pet => pet.isActive) ?? [], [overview]);
  const selectedPet = overview?.pets.find(pet => pet.id === selectedPetId) ?? null;
  const openChat = (item: PatiMatchCandidate) => { setChatMatch(item); onChatStateChange(true); };
  const closeChat = () => { setChatMatch(null); onChatStateChange(false); };

  useEffect(() => () => onChatStateChange(false), [onChatStateChange]);

  const load = useCallback(async (refresh = false) => {
    if (!token) return;
    refresh ? setRefreshing(true) : setLoading(true);
    setError(null);
    try {
      const result = await getPatiMatchOverview(token);
      setOverview(result);
      const active = result.pets.filter(pet => pet.isActive);
      setSelectedPetId(current => result.pets.some(pet => pet.id === current) ? current : (active[0]?.id ?? result.pets[0]?.id ?? null));
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'PatiMatch açılamadı.'); }
    finally { setLoading(false); setRefreshing(false); }
  }, [token]);

  useEffect(() => { void load(); }, [load]);

  useEffect(() => {
    if (!token || !initialTargetPetId || !overview || openedInitialTargetId === initialTargetPetId) return;
    let active = true;
    void (async () => {
      for (const pet of overview.pets.filter(item => item.isActive)) {
        const petMatches = await getPatiMatches(token, pet.id).catch(() => []);
        const target = petMatches.find(item => item.petId === initialTargetPetId);
        if (active && target) {
          setSelectedPetId(pet.id);
          setMatches(petMatches);
          setChatMatch(target);
          setOpenedInitialTargetId(initialTargetPetId);
          onChatStateChange(true);
          break;
        }
      }
    })();
    return () => { active = false; };
  }, [initialTargetPetId, onChatStateChange, openedInitialTargetId, overview, token]);

  useEffect(() => {
    if (!selectedPet || (selectedPet.isActive && editingPetId !== selectedPet.id)) return;
    setCity(selectedPet.city ?? '');
    setDistrict(selectedPet.district ?? '');
    setSelectedCityId(selectedPet.city ? `saved-city-${selectedPet.id}` : null);
    setSelectedDistrictId(selectedPet.district ? `saved-district-${selectedPet.id}` : null);
    setPreferredTypes(selectedPet.preferredTypes?.length ? selectedPet.preferredTypes : [selectedPet.type]);
  }, [editingPetId, selectedPet?.id, selectedPet?.isActive]);

  useEffect(() => {
    if (!token || !selectedPet?.isActive) { setCandidates([]); setMatches([]); return; }
    const controller = new AbortController();
    Promise.all([getPatiMatchCandidates(token, selectedPet.id, controller.signal), getPatiMatches(token, selectedPet.id, controller.signal)])
      .then(([nextCandidates, nextMatches]) => { setCandidates(nextCandidates); setMatches(nextMatches); })
      .catch(reason => { if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : 'PatiMatch akışı alınamadı.'); });
    return () => controller.abort();
  }, [token, selectedPet?.id, selectedPet?.isActive]);

  const enroll = async () => {
    if (!token || !selectedPet) return;
    if (!city.trim()) return Alert.alert('Şehir gerekli', 'Yaklaşık eşleşme alanı için yalnızca şehrini yaz.');
    if (!selectedCityId) return Alert.alert('Şehri listeden seç', 'Şehir adını yazdıktan sonra webden gelen önerilerden birini seçmelisin.');
    if (district.trim() && !selectedDistrictId) return Alert.alert('İlçeyi listeden seç', 'İlçe adını yazdıktan sonra webden gelen önerilerden birini seçmelisin.');
    if (purpose === 'friendship' && preferredTypes.length === 0) return Alert.alert('Pati türü seç', 'Karşına çıkmasını istediğin en az bir pati türünü seçmelisin.');
    if (!accepted) return Alert.alert('Güvenlik onayı gerekli', 'PatiMatch güvenlik kurallarını kabul etmelisin.');
    setBusy(true);
    try {
      const saved = await enrollPatiMatch(token, { petId: selectedPet.id, purpose, city: city.trim(), district: district.trim() || undefined, preferredTypes: purpose === 'mate' ? [selectedPet.type] : preferredTypes, acceptSafetyTerms: accepted });
      setOverview(current => current ? { ...current, pets: current.pets.map(pet => pet.id === saved.id ? saved : pet) } : current);
      setEditingPetId(null);
      setAccepted(false);
      Alert.alert('PatiMatch hazır', `${saved.name} artık gerçek PatiMatch akışında görünebilir.`);
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : 'Lütfen tekrar dene.';
      if (message.includes('(401)')) {
        Alert.alert('Oturumun yenilenmeli', 'Güvenliğin için yeniden giriş yapmalısın.', [
          { text: 'Giriş yap', onPress: onSessionExpired },
        ]);
      } else Alert.alert('Katılım tamamlanamadı', message);
    }
    finally { setBusy(false); }
  };

  const decide = async (candidate: PatiMatchCandidate, isLike: boolean) => {
    if (!token || !selectedPet || busy) return false;
    setBusy(true);
    try {
      const result = await decidePatiMatch(token, selectedPet.id, candidate.petId, isLike);
      setCandidates(current => current.filter(item => item.petId !== candidate.petId));
      if (result.matched) {
        setMatches(current => [candidate, ...current.filter(item => item.petId !== candidate.petId)]);
        Alert.alert('Bir PatiMatch oldu! 💜', `${selectedPet.name} ve ${candidate.name} birbirini beğendi. Artık güvenli sohbeti başlatabilirsin.`, [
          { text: 'Daha sonra', style: 'cancel' },
          { text: 'Sohbete git', onPress: () => openChat(candidate) },
        ]);
      }
      return true;
    } catch (reason) { Alert.alert('Seçim kaydedilemedi', reason instanceof Error ? reason.message : 'Lütfen tekrar dene.'); return false; }
    finally { setBusy(false); }
  };

  const deactivate = () => {
    if (!token || !selectedPet) return;
    Alert.alert('PatiMatch profilini kapat', `${selectedPet.name} artık adaylarda görünmeyecek.`, [
      { text: 'Vazgeç', style: 'cancel' },
      { text: 'Profili kapat', style: 'destructive', onPress: async () => {
        try { await deactivatePatiMatch(token, selectedPet.id); await load(); }
        catch (reason) { Alert.alert('Kapatılamadı', reason instanceof Error ? reason.message : 'Lütfen tekrar dene.'); }
      } },
    ]);
  };

  if (!token || !username) return <GuestState onLogin={onLogin} />;
  if (selectedPet && chatMatch) return <PatiMatchChatScreen token={token} sourcePet={selectedPet} match={chatMatch} onBack={closeChat} />;

  return <ScrollView style={styles.screen} contentContainerStyle={styles.content} showsVerticalScrollIndicator={false} refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load(true)} tintColor={colors.primary} />}>
    <Header matchCount={overview?.matchCount ?? 0} />
    {loading ? <View style={styles.loading}><ActivityIndicator size="large" color={colors.primary} /><Text style={styles.muted}>PatiMatch hazırlanıyor…</Text></View> : null}
    {!loading && error ? <View style={styles.errorCard}><Ionicons name="cloud-offline-outline" size={27} color={colors.danger} /><Text style={styles.errorText}>{error}</Text><Pressable onPress={() => void load()} style={styles.smallButton}><Text style={styles.smallButtonText}>Tekrar dene</Text></Pressable></View> : null}
    {!loading && !error && !overview?.pets.length ? <EmptyPets onOpenPets={onOpenPets} /> : null}
    {!loading && !error && overview?.pets.length ? <>
      <Text style={styles.sectionLabel}>HANGİ PATİN?</Text>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.petPicker}>
        {overview.pets.map(pet => <PetChip key={pet.id} pet={pet} selected={pet.id === selectedPetId} onPress={() => { setSelectedPetId(pet.id); setPurpose(pet.purpose ?? 'friendship'); setPreferredTypes(pet.preferredTypes?.length ? pet.preferredTypes : [pet.type]); }} />)}
      </ScrollView>
      {selectedPet && (!selectedPet.isActive || editingPetId === selectedPet.id) ? <JoinCard pet={selectedPet} city={city} district={district} selectedCityId={selectedCityId} selectedDistrictId={selectedDistrictId} purpose={purpose} preferredTypes={preferredTypes} accepted={accepted} busy={busy} onCity={value => { setCity(value); setSelectedCityId(null); setDistrict(''); setSelectedDistrictId(null); }} onCitySelect={suggestion => { setCity(suggestion.name); setSelectedCityId(suggestion.id); setDistrict(''); setSelectedDistrictId(null); }} onDistrict={value => { setDistrict(value); setSelectedDistrictId(null); }} onDistrictSelect={suggestion => { setDistrict(suggestion.name); setSelectedDistrictId(suggestion.id); }} onPurpose={value => { setPurpose(value); if (value === 'mate') setPreferredTypes([selectedPet.type]); }} onToggleType={value => setPreferredTypes(current => current.includes(value) ? current.filter(item => item !== value) : [...current, value])} onAccepted={() => setAccepted(value => !value)} onEnroll={() => void enroll()} onOpenPets={onOpenPets} /> : null}
      {selectedPet?.isActive && editingPetId !== selectedPet.id ? <>
        <View style={styles.activeBar}><View style={styles.flex}><Text style={styles.activeTitle}>{selectedPet.name} ile keşfet</Text><Text style={styles.activeMeta}>{purposeLabels[selectedPet.purpose ?? 'friendship']} · {(selectedPet.preferredTypes?.length ? selectedPet.preferredTypes : [selectedPet.type]).join(', ')} · {[selectedPet.city, selectedPet.district].filter(Boolean).join(', ')}</Text></View><View style={styles.activeActions}><Pressable onPress={() => { setEditingPetId(selectedPet.id); setAccepted(false); }}><Text style={styles.editProfile}>Düzenle</Text></Pressable><Pressable onPress={deactivate}><Text style={styles.closeProfile}>Kapat</Text></Pressable></View></View>
        {candidates[0] ? <><Text style={styles.swipeHint}>Sola kaydır: geç · Sağa kaydır: beğen</Text><SwipeCard key={candidates[0].petId} candidate={candidates[0]} busy={busy} onDecision={isLike => decide(candidates[0], isLike)} /><View style={styles.actions}><Pressable disabled={busy} onPress={() => void decide(candidates[0], false)} style={[styles.actionButton, styles.passButton]}><Ionicons name="close" size={31} color="#A65345" /></Pressable><Pressable disabled={busy} onPress={() => void decide(candidates[0], true)} style={[styles.actionButton, styles.likeButton]}><Ionicons name="heart" size={29} color={colors.white} /></Pressable></View></> : <NoCandidates onRefresh={() => void load(true)} />}
        <Matches items={matches} onOpen={openChat} />
      </> : null}
    </> : null}
    <SafetyNote />
  </ScrollView>;
}

function Header({ matchCount }: { matchCount: number }) { return <View style={styles.header}><View><Text style={styles.eyebrow}>PET’İM</Text><Text style={styles.title}>PatiMatch</Text><Text style={styles.subtitle}>Patine güvenli bir arkadaşlık alanı.</Text></View><View style={styles.matchPill}><Ionicons name="heart" size={18} color={colors.primary} /><Text style={styles.matchPillText}>{matchCount}</Text></View></View>; }

function GuestState({ onLogin }: { onLogin: () => void }) { return <View style={[styles.screen, styles.centerContent]}><View style={styles.heroIcon}><Ionicons name="heart" size={48} color={colors.primary} /></View><Text style={styles.title}>PatiMatch</Text><Text style={styles.guestText}>Pati profilinle güvenli eşleşmeleri keşfetmek için PetWork hesabınla giriş yap.</Text><Pressable onPress={onLogin} style={styles.primaryButton}><Text style={styles.primaryButtonText}>Giriş yap</Text></Pressable></View>; }

function EmptyPets({ onOpenPets }: { onOpenPets: () => void }) { return <View style={styles.emptyCard}><View style={styles.heroIcon}><Ionicons name="paw" size={42} color={colors.primary} /></View><Text style={styles.cardTitle}>Önce bir pati profili</Text><Text style={styles.cardText}>PatiMatch’e katılmak istediğinde pati bilgileri gerekir. Uygulamanın diğer alanlarını kullanmak için zorunlu değildir.</Text><Pressable onPress={onOpenPets} style={styles.primaryButton}><Ionicons name="add" size={19} color={colors.white} /><Text style={styles.primaryButtonText}>Patimi ekle</Text></Pressable></View>; }

function PetChip({ pet, selected, onPress }: { pet: PatiMatchMyPet; selected: boolean; onPress: () => void }) { return <Pressable onPress={onPress} style={[styles.petChip, selected && styles.petChipSelected]}><Image source={{ uri: mediaUrl(pet.profileImage) }} style={styles.petChipImage} /><View><Text style={[styles.petChipName, selected && styles.petChipNameSelected]}>{pet.name}</Text><Text style={[styles.petChipState, selected && styles.petChipNameSelected]}>{pet.isActive ? 'Aktif' : 'Katılmadı'}</Text></View></Pressable>; }

type JoinProps = { pet: PatiMatchMyPet; city: string; district: string; selectedCityId: string | null; selectedDistrictId: string | null; purpose: 'friendship' | 'mate'; preferredTypes: string[]; accepted: boolean; busy: boolean; onCity: (v: string) => void; onCitySelect: (v: LocationSuggestion) => void; onDistrict: (v: string) => void; onDistrictSelect: (v: LocationSuggestion) => void; onPurpose: (v: 'friendship' | 'mate') => void; onToggleType: (v: string) => void; onAccepted: () => void; onEnroll: () => void; onOpenPets: () => void };
function JoinCard({ pet, city, district, selectedCityId, selectedDistrictId, purpose, preferredTypes, accepted, busy, onCity, onCitySelect, onDistrict, onDistrictSelect, onPurpose, onToggleType, onAccepted, onEnroll, onOpenPets }: JoinProps) {
  const hasPhoto = Boolean(pet.profileImage && pet.profileImage !== 'img/pet-default.jpg');
  return <View style={styles.joinCard}><Text style={styles.cardTitle}>{pet.name} için PatiMatch {pet.isActive ? 'tercihlerini düzenle' : 'oluştur'}</Text><Text style={styles.cardText}>Yalnızca yaklaşık konum gösterilir. Açık adresin ve iletişim bilgin paylaşılmaz.</Text>
    {!hasPhoto ? <Pressable onPress={onOpenPets} style={styles.photoWarning}><Ionicons name="camera-outline" size={22} color="#A65345" /><View style={styles.flex}><Text style={styles.warningTitle}>Gerçek bir fotoğraf gerekli</Text><Text style={styles.warningText}>Patilerim ekranından fotoğraf ekle.</Text></View><Ionicons name="chevron-forward" size={19} color="#A65345" /></Pressable> : null}
    <Text style={styles.fieldLabel}>Aradığınız</Text><View style={styles.purposeRow}>{(['friendship', 'mate'] as const).map(value => <Pressable key={value} onPress={() => onPurpose(value)} style={[styles.purposeChip, purpose === value && styles.purposeActive]}><Text style={[styles.purposeText, purpose === value && styles.purposeTextActive]}>{purposeLabels[value]}</Text></Pressable>)}</View>
    <Text style={styles.fieldLabel}>Karşına hangi patiler çıksın?</Text>
    {purpose === 'mate' ? <View style={styles.mateTypeNote}><Ionicons name="information-circle-outline" size={18} color="#4E7458" /><Text style={styles.mateTypeText}>Eş aramada hayvan refahı için yalnızca aynı tür ({pet.type}) gösterilir.</Text></View> : <View style={styles.typeChoices}>{petTypes.map(type => { const active = preferredTypes.includes(type); return <Pressable key={type} onPress={() => onToggleType(type)} style={[styles.typeChoice, active && styles.typeChoiceActive]}><Ionicons name={active ? 'checkmark-circle' : 'ellipse-outline'} size={16} color={active ? colors.white : colors.primary} /><Text style={[styles.typeChoiceText, active && styles.typeChoiceTextActive]}>{type}</Text></Pressable>; })}</View>}
    <LocationAutocomplete label="Şehir *" value={city} selectedId={selectedCityId} kind="city" placeholder="Yaz ve listeden seç" onChange={onCity} onSelect={onCitySelect} />
    <LocationAutocomplete label="İlçe (isteğe bağlı)" value={district} selectedId={selectedDistrictId} kind="district" city={city} placeholder={selectedCityId ? 'Yaz ve listeden seç' : 'Önce şehir seç'} enabled={Boolean(selectedCityId)} onChange={onDistrict} onSelect={onDistrictSelect} />
    <Pressable onPress={onAccepted} style={styles.consentRow}><View style={[styles.checkbox, accepted && styles.checkboxActive]}>{accepted ? <Ionicons name="checkmark" size={16} color={colors.white} /> : null}</View><Text style={styles.consentText}>Sorumlu buluşma, güvenli alan ve hayvan refahı kurallarını kabul ediyorum.</Text></Pressable>
    <Pressable disabled={busy || !hasPhoto} onPress={onEnroll} style={[styles.primaryButton, (!hasPhoto || busy) && styles.disabled]}>{busy ? <ActivityIndicator color={colors.white} /> : <><Ionicons name="heart-outline" size={19} color={colors.white} /><Text style={styles.primaryButtonText}>{pet.isActive ? 'Tercihleri kaydet' : 'PatiMatch’e katıl'}</Text></>}</Pressable>
  </View>;
}

type LocationAutocompleteProps = {
  label: string; value: string; selectedId: string | null; kind: 'city' | 'district'; city?: string;
  placeholder: string; enabled?: boolean; onChange: (value: string) => void; onSelect: (suggestion: LocationSuggestion) => void;
};

function LocationAutocomplete({ label, value, selectedId, kind, city, placeholder, enabled = true, onChange, onSelect }: LocationAutocompleteProps) {
  const [suggestions, setSuggestions] = useState<LocationSuggestion[]>([]);
  const [loadingSuggestions, setLoadingSuggestions] = useState(false);
  const [lookupError, setLookupError] = useState<string | null>(null);

  useEffect(() => {
    if (!enabled || selectedId || value.trim().length < 2) {
      setSuggestions([]); setLookupError(null); setLoadingSuggestions(false); return;
    }
    let controller: AbortController | null = null;
    const timer = setTimeout(() => {
      controller = new AbortController(); setLoadingSuggestions(true); setLookupError(null);
      getLocationSuggestions(value, kind, city, controller.signal)
        .then(setSuggestions)
        .catch(reason => {
          if (!controller?.signal.aborted) { setSuggestions([]); setLookupError(reason instanceof Error ? reason.message : 'Konumlar alınamadı.'); }
        })
        .finally(() => { if (!controller?.signal.aborted) setLoadingSuggestions(false); });
    }, 350);
    return () => { clearTimeout(timer); controller?.abort(); };
  }, [city, enabled, kind, selectedId, value]);

  const showPanel = enabled && !selectedId && value.trim().length >= 2;
  return <View>
    <Text style={styles.fieldLabel}>{label}</Text>
    <View style={[locationStyles.input, !enabled && styles.disabled]}>
      <Ionicons name="location-outline" size={18} color={colors.primary} />
      <TextInput value={value} editable={enabled} onChangeText={onChange} maxLength={80} autoCorrect={false} placeholder={placeholder} placeholderTextColor="#A09599" style={locationStyles.textInput} />
      {loadingSuggestions ? <ActivityIndicator size="small" color={colors.primary} /> : selectedId ? <Ionicons name="checkmark-circle" size={19} color="#4E7458" /> : null}
    </View>
    {showPanel ? <View style={locationStyles.panel}>
      {suggestions.map(suggestion => <Pressable key={suggestion.id} onPress={() => onSelect(suggestion)} style={locationStyles.row}>
        <Ionicons name="location" size={17} color={colors.primary} />
        <View style={styles.flex}><Text style={locationStyles.name}>{suggestion.name}</Text>{suggestion.secondaryText ? <Text style={locationStyles.secondary}>{suggestion.secondaryText}</Text> : null}</View>
      </Pressable>)}
      {!loadingSuggestions && !lookupError && suggestions.length === 0 ? <Text style={locationStyles.message}>Eşleşen konum bulunamadı.</Text> : null}
      {lookupError ? <Text style={[locationStyles.message, locationStyles.error]}>{lookupError}</Text> : null}
      <Text style={locationStyles.attribution}>Google tarafından sağlanır</Text>
    </View> : null}
  </View>;
}

function SwipeCard({ candidate, busy, onDecision }: { candidate: PatiMatchCandidate; busy: boolean; onDecision: (like: boolean) => Promise<boolean> }) {
  const position = useRef(new Animated.ValueXY()).current;
  const rotate = position.x.interpolate({ inputRange: [-220, 0, 220], outputRange: ['-9deg', '0deg', '9deg'] });
  const swipe = useCallback(async (like: boolean, fromGesture = false) => {
    if (busy) return;
    const target = like ? 500 : -500;
    if (!fromGesture) await new Promise<void>(resolve => Animated.timing(position, { toValue: { x: target, y: 0 }, duration: 210, useNativeDriver: true }).start(() => resolve()));
    const saved = await onDecision(like);
    if (!saved) Animated.spring(position, { toValue: { x: 0, y: 0 }, useNativeDriver: true }).start();
  }, [busy, onDecision, position]);
  const panResponder = useMemo(() => PanResponder.create({ onMoveShouldSetPanResponder: (_, gesture) => Math.abs(gesture.dx) > 8, onPanResponderMove: Animated.event([null, { dx: position.x, dy: position.y }], { useNativeDriver: false }), onPanResponderRelease: (_, gesture) => { if (Math.abs(gesture.dx) > 90) { const like = gesture.dx > 0; Animated.timing(position, { toValue: { x: like ? 500 : -500, y: gesture.dy }, duration: 180, useNativeDriver: true }).start(() => void swipe(like, true)); } else Animated.spring(position, { toValue: { x: 0, y: 0 }, useNativeDriver: true }).start(); } }), [position, swipe]);
  return <Animated.View {...panResponder.panHandlers} style={[styles.swipeCard, { transform: [...position.getTranslateTransform(), { rotate }] }]}>
    <Image source={{ uri: mediaUrl(candidate.profileImage) }} style={styles.candidateImage} resizeMode="cover" />
    <View style={styles.candidateBody}><View style={styles.candidateTitleRow}><Text style={styles.candidateName}>{candidate.name}{candidate.age != null ? `, ${String(candidate.age).replace('.', ',')}` : ''}</Text><View style={styles.purposeBadge}><Text style={styles.purposeBadgeText}>{purposeLabels[candidate.purpose]}</Text></View></View><Text style={styles.candidateMeta}>{[candidate.type, candidate.breed, candidate.gender].filter(Boolean).join(' · ')}</Text><View style={styles.locationRow}><Ionicons name="location-outline" size={16} color={colors.primary} /><Text style={styles.locationText}>{[candidate.city, candidate.district].filter(Boolean).join(', ')}</Text></View>{candidate.description ? <Text style={styles.candidateDescription}>{candidate.description}</Text> : null}</View>
  </Animated.View>;
}

function NoCandidates({ onRefresh }: { onRefresh: () => void }) { return <View style={styles.emptyCard}><Ionicons name="sparkles-outline" size={37} color={colors.primary} /><Text style={styles.cardTitle}>Şimdilik yeni pati yok</Text><Text style={styles.cardText}>Yalnızca PatiMatch’e gerçekten katılan profiller burada görünür. Daha sonra tekrar kontrol edebilirsin.</Text><Pressable onPress={onRefresh} style={styles.smallButton}><Text style={styles.smallButtonText}>Yenile</Text></Pressable></View>; }
function Matches({ items, onOpen }: { items: PatiMatchCandidate[]; onOpen: (item: PatiMatchCandidate) => void }) { if (!items.length) return null; return <View><Text style={styles.sectionLabel}>EŞLEŞMELERİN</Text><ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.matchesRow}>{items.map(item => <Pressable key={item.petId} onPress={() => onOpen(item)} accessibilityRole="button" accessibilityLabel={`${item.name} ile sohbeti aç`} style={({ pressed }) => [styles.matchCard, pressed && { opacity: .72 }]}><Image source={{ uri: mediaUrl(item.profileImage) }} style={styles.matchImage} /><Text style={styles.matchName}>{item.name}</Text><Text style={styles.matchLocation}>{item.city}</Text><View style={styles.chatLink}><Ionicons name="chatbubble-ellipses-outline" size={13} color={colors.primary} /><Text style={styles.chatLinkText}>Sohbeti aç</Text></View></Pressable>)}</ScrollView></View>; }
function SafetyNote() { return <View style={styles.safetyNote}><Ionicons name="shield-checkmark-outline" size={24} color="#4E7458" /><Text style={styles.safetyText}>İlk buluşmayı halka açık bir yerde yap. Sağlık, aşı ve kısırlaştırma bilgilerini yüz yüze doğrula; hayvan refahını her zaman eşleşmenin önünde tut.</Text></View>; }

const serif = Platform.select({ ios: 'Georgia', android: 'serif', default: 'serif' });
const locationStyles = createThemedStyles(() => ({
  input:{minHeight:51,flexDirection:'row',alignItems:'center',gap:9,borderRadius:16,borderWidth:1,borderColor:colors.border,backgroundColor:colors.background,paddingHorizontal:14},
  textInput:{flex:1,minHeight:49,color:colors.text},
  panel:{marginTop:6,borderRadius:16,borderWidth:1,borderColor:colors.border,backgroundColor:colors.card,overflow:'hidden'},
  row:{flexDirection:'row',alignItems:'center',gap:10,paddingHorizontal:14,paddingVertical:11,borderBottomWidth:1,borderBottomColor:colors.border},
  name:{color:colors.text,fontSize:11,fontWeight:'900'},
  secondary:{color:colors.muted,fontSize:9,marginTop:2},
  message:{color:colors.muted,fontSize:10,padding:13,textAlign:'center'},
  error:{color:colors.danger},
  attribution:{color:colors.muted,fontSize:8,textAlign:'right',paddingHorizontal:12,paddingVertical:7},
}));
const styles = createThemedStyles(() => ({
  screen:{flex:1,backgroundColor:colors.background},content:{paddingTop:Platform.OS==='ios'?58:30,paddingHorizontal:20,paddingBottom:125,maxWidth:680,width:'100%',alignSelf:'center'},centerContent:{alignItems:'center',justifyContent:'center',padding:34},header:{flexDirection:'row',alignItems:'center',justifyContent:'space-between',marginBottom:24},eyebrow:{color:colors.peach,fontSize:10,fontWeight:'900',letterSpacing:2},title:{fontFamily:serif,fontSize:36,fontWeight:'700',color:colors.text},subtitle:{color:colors.muted,fontSize:11,marginTop:3},matchPill:{flexDirection:'row',alignItems:'center',gap:6,backgroundColor:colors.lilacSoft,borderRadius:22,paddingHorizontal:15,paddingVertical:11},matchPillText:{color:colors.primary,fontWeight:'900'},loading:{minHeight:320,alignItems:'center',justifyContent:'center',gap:12},muted:{color:colors.muted},errorCard:{alignItems:'center',gap:12,backgroundColor:colors.peachSoft,borderRadius:24,padding:25},errorText:{color:colors.text,textAlign:'center'},smallButton:{backgroundColor:colors.lilacSoft,borderRadius:14,paddingHorizontal:17,paddingVertical:10,marginTop:9},smallButtonText:{color:colors.primary,fontWeight:'900'},heroIcon:{width:90,height:90,borderRadius:45,backgroundColor:colors.lilacSoft,alignItems:'center',justifyContent:'center',marginBottom:16},guestText:{color:colors.muted,textAlign:'center',lineHeight:20,maxWidth:340,marginTop:10},sectionLabel:{color:colors.primary,fontSize:10,fontWeight:'900',letterSpacing:1.5,marginTop:18,marginBottom:10},petPicker:{gap:10,paddingRight:8},petChip:{flexDirection:'row',alignItems:'center',gap:8,backgroundColor:colors.card,borderWidth:1,borderColor:colors.border,borderRadius:20,padding:8,paddingRight:14},petChipSelected:{backgroundColor:colors.primary,borderColor:colors.primary},petChipImage:{width:39,height:39,borderRadius:14},petChipName:{color:colors.text,fontSize:11,fontWeight:'900'},petChipState:{color:colors.muted,fontSize:8,marginTop:2},petChipNameSelected:{color:colors.white},joinCard:{backgroundColor:colors.card,borderRadius:26,padding:19,marginTop:17,borderWidth:1,borderColor:colors.border,...shadow},cardTitle:{fontFamily:serif,fontSize:23,fontWeight:'700',color:colors.text,marginTop:9},cardText:{color:colors.muted,fontSize:11,lineHeight:17,marginTop:6},photoWarning:{flexDirection:'row',alignItems:'center',gap:10,backgroundColor:colors.peachSoft,borderRadius:17,padding:13,marginTop:15},warningTitle:{color:'#A65345',fontWeight:'900',fontSize:11},warningText:{color:'#A65345',fontSize:9,marginTop:2},flex:{flex:1},fieldLabel:{color:colors.text,fontSize:11,fontWeight:'900',marginTop:16,marginBottom:7},purposeRow:{flexDirection:'row',gap:9},purposeChip:{flex:1,alignItems:'center',padding:12,borderRadius:15,borderWidth:1,borderColor:colors.border},purposeActive:{backgroundColor:colors.primary,borderColor:colors.primary},purposeText:{color:colors.muted,fontWeight:'800',fontSize:10},purposeTextActive:{color:colors.white},typeChoices:{flexDirection:'row',flexWrap:'wrap',gap:8},typeChoice:{flexDirection:'row',alignItems:'center',gap:5,borderWidth:1,borderColor:colors.border,borderRadius:14,paddingHorizontal:11,paddingVertical:9,backgroundColor:colors.background},typeChoiceActive:{backgroundColor:colors.primary,borderColor:colors.primary},typeChoiceText:{color:colors.text,fontSize:10,fontWeight:'800'},typeChoiceTextActive:{color:colors.white},mateTypeNote:{flexDirection:'row',alignItems:'center',gap:8,backgroundColor:colors.sageSoft,borderRadius:14,padding:12},mateTypeText:{flex:1,color:'#4E7458',fontSize:9.5,lineHeight:15,fontWeight:'700'},input:{minHeight:51,borderRadius:16,borderWidth:1,borderColor:colors.border,backgroundColor:colors.background,paddingHorizontal:15,color:colors.text},consentRow:{flexDirection:'row',alignItems:'flex-start',gap:10,marginTop:17},checkbox:{width:24,height:24,borderRadius:7,borderWidth:1.4,borderColor:colors.primary,alignItems:'center',justifyContent:'center'},checkboxActive:{backgroundColor:colors.primary},consentText:{flex:1,color:colors.muted,fontSize:10,lineHeight:15},primaryButton:{minHeight:53,flexDirection:'row',gap:8,alignItems:'center',justifyContent:'center',backgroundColor:colors.primary,borderRadius:18,paddingHorizontal:24,marginTop:20},primaryButtonText:{color:colors.white,fontWeight:'900'},disabled:{opacity:.42},activeBar:{flexDirection:'row',alignItems:'center',justifyContent:'space-between',gap:10,backgroundColor:colors.sageSoft,borderRadius:20,padding:15,marginTop:17},activeTitle:{color:colors.text,fontWeight:'900'},activeMeta:{color:'#4E7458',fontSize:9,lineHeight:14,marginTop:4},activeActions:{alignItems:'flex-end',gap:9},editProfile:{color:'#4E7458',fontSize:9,fontWeight:'900'},closeProfile:{color:colors.primary,fontSize:9,fontWeight:'900'},swipeHint:{color:colors.muted,textAlign:'center',fontSize:9,marginVertical:13},swipeCard:{backgroundColor:colors.card,borderRadius:29,overflow:'hidden',borderWidth:1,borderColor:colors.border,...shadow},candidateImage:{width:'100%',height:360,backgroundColor:colors.lilacSoft},candidateBody:{padding:18},candidateTitleRow:{flexDirection:'row',alignItems:'center',justifyContent:'space-between',gap:8},candidateName:{flex:1,fontFamily:serif,fontSize:27,fontWeight:'700',color:colors.text},purposeBadge:{backgroundColor:colors.lilacSoft,borderRadius:13,paddingHorizontal:10,paddingVertical:6},purposeBadgeText:{color:colors.primary,fontSize:9,fontWeight:'900'},candidateMeta:{color:colors.muted,fontSize:11,marginTop:5},locationRow:{flexDirection:'row',alignItems:'center',gap:5,marginTop:11},locationText:{color:colors.primary,fontSize:10,fontWeight:'800'},candidateDescription:{color:colors.text,fontSize:11,lineHeight:17,marginTop:12},actions:{flexDirection:'row',justifyContent:'center',gap:28,marginTop:17},actionButton:{width:64,height:64,borderRadius:32,alignItems:'center',justifyContent:'center',...shadow},passButton:{backgroundColor:colors.card,borderWidth:1,borderColor:colors.peach},likeButton:{backgroundColor:colors.primary},emptyCard:{alignItems:'center',backgroundColor:colors.card,borderRadius:25,padding:27,marginTop:17,borderWidth:1,borderColor:colors.border,...shadow},matchesRow:{gap:11,paddingRight:8},matchCard:{width:104,backgroundColor:colors.card,borderRadius:18,padding:8,borderWidth:1,borderColor:colors.border},matchImage:{width:86,height:86,borderRadius:14},matchName:{color:colors.text,fontWeight:'900',fontSize:11,marginTop:7},matchLocation:{color:colors.muted,fontSize:8,marginTop:2},chatLink:{flexDirection:'row',alignItems:'center',gap:4,marginTop:7},chatLinkText:{color:colors.primary,fontSize:8,fontWeight:'900'},safetyNote:{flexDirection:'row',alignItems:'flex-start',gap:11,backgroundColor:colors.sageSoft,borderRadius:20,padding:16,marginTop:24},safetyText:{flex:1,color:'#4E7458',fontSize:9,lineHeight:15}
}));
