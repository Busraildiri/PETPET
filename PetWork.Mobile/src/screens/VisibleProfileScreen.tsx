import { Ionicons } from '@expo/vector-icons';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Image, Platform, Pressable, ScrollView, Text, View } from 'react-native';
import { getMatchedUserProfile, getPublicUserProfile, mediaUrl, type PetProfile, type VisibleUserProfile } from '../api';
import { colors, createThemedStyles, getThemeMode, shadow } from '../theme';

type Props = {
  token?: string | null;
  userId?: number;
  sourcePetId?: number;
  targetPetId?: number;
  onBack: () => void;
};

export function VisibleProfileScreen({ token, userId, sourcePetId, targetPetId, onBack }: Props) {
  const [profile, setProfile] = useState<VisibleUserProfile | null>(null);
  const [selectedPet, setSelectedPet] = useState<(PetProfile & { isMatchedPet: boolean }) | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    setProfile(null); setError(null); setSelectedPet(null);
    const request = sourcePetId && targetPetId && token
      ? getMatchedUserProfile(token, sourcePetId, targetPetId, controller.signal)
      : userId ? getPublicUserProfile(userId, token, controller.signal) : Promise.reject(new Error('Profil bilgisi eksik.'));
    request.then(setProfile).catch(reason => { if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : 'Profil yüklenemedi.'); });
    return () => controller.abort();
  }, [sourcePetId, targetPetId, token, userId]);

  if (selectedPet) return <PetDetail pet={selectedPet} onBack={() => setSelectedPet(null)} />;
  return <ScrollView style={styles.screen} contentContainerStyle={styles.content} showsVerticalScrollIndicator={false}>
    <StatusBar style={getThemeMode() === 'dark' ? 'light' : 'dark'} />
    <View style={styles.header}><Pressable onPress={onBack} style={styles.back}><Ionicons name="arrow-back" size={23} color={colors.primary} /></Pressable><Text style={styles.headerTitle}>Kullanıcı profili</Text><View style={styles.headerSpace} /></View>
    {!profile && !error ? <ActivityIndicator style={styles.loading} size="large" color={colors.primary} /> : null}
    {error ? <View style={styles.errorCard}><Ionicons name="lock-closed-outline" size={30} color={colors.danger} /><Text style={styles.errorText}>{error}</Text></View> : null}
    {profile ? <>
      <View style={styles.profileCard}>
        <Image source={{ uri: mediaUrl(profile.profileImage) }} style={styles.avatar} />
        <Text style={styles.username}>@{profile.username}</Text>
        {profile.isBioVisible ? <Text style={styles.bio}>{profile.bio?.trim() || 'Henüz hakkında bilgisi eklenmedi.'}</Text> : null}
      </View>
      <Text style={styles.sectionTitle}>Patileri</Text>
      {!profile.pets.length ? <Text style={styles.empty}>{profile.arePetsVisible ? 'Herkese açık pati profili bulunmuyor.' : 'Kullanıcı pati bilgilerini gizli tutuyor.'}</Text> : profile.pets.map(pet => <Pressable key={pet.id} onPress={() => setSelectedPet(pet)} style={styles.petCard}>
        <Image source={{ uri: mediaUrl(pet.profileImage) }} style={styles.petImage} />
        <View style={styles.flex}><View style={styles.nameRow}><Text style={styles.petName}>{pet.name}</Text>{pet.isMatchedPet ? <Text style={styles.matchBadge}>Eşleşen pati</Text> : null}</View><Text style={styles.petMeta}>{[pet.type, pet.breed, pet.gender].filter(Boolean).join(' · ')}</Text><Text numberOfLines={2} style={styles.petAbout}>{pet.description || 'Henüz hakkında bilgisi eklenmedi.'}</Text></View>
        <Ionicons name="chevron-forward" size={20} color={colors.muted} />
      </Pressable>)}
    </> : null}
  </ScrollView>;
}

function PetDetail({ pet, onBack }: { pet: PetProfile & { isMatchedPet: boolean }; onBack: () => void }) {
  const facts = [
    ['Karakter', pet.character], ['Bakım notu', pet.careNotes], ['Çocuklarla uyum', pet.childCompatibility],
    ['Diğer hayvanlarla uyum', pet.otherPetCompatibility], ['Aşı', boolLabel(pet.isVaccinated)],
    ['Kısırlaştırma', boolLabel(pet.isNeutered)], ['Mikroçip', boolLabel(pet.isMicrochipped)],
    ...Object.entries(pet.extraAttributes || {}),
  ].filter(([, value]) => Boolean(value));
  return <ScrollView style={styles.screen} contentContainerStyle={styles.content} showsVerticalScrollIndicator={false}>
    <StatusBar style={getThemeMode() === 'dark' ? 'light' : 'dark'} />
    <View style={styles.header}><Pressable onPress={onBack} style={styles.back}><Ionicons name="arrow-back" size={23} color={colors.primary} /></Pressable><Text style={styles.headerTitle}>Pati profili</Text><View style={styles.headerSpace} /></View>
    <Image source={{ uri: mediaUrl(pet.profileImage) }} style={styles.heroImage} />
    <Text style={styles.detailName}>{pet.name}</Text><Text style={styles.detailMeta}>{[pet.type, pet.breed, pet.age != null ? `${pet.age} yaş` : null, pet.gender].filter(Boolean).join(' · ')}</Text>
    <View style={styles.aboutCard}><Text style={styles.sectionTitle}>Hakkında</Text><Text style={styles.detailText}>{pet.description || 'Henüz hakkında bilgisi eklenmedi.'}</Text></View>
    {facts.map(([label, value]) => <View key={label} style={styles.fact}><Text style={styles.factLabel}>{label}</Text><Text style={styles.factValue}>{value}</Text></View>)}
    {pet.tags?.length ? <View style={styles.tags}>{pet.tags.map(tag => <Text key={tag} style={styles.tag}>#{tag}</Text>)}</View> : null}
  </ScrollView>;
}

const boolLabel = (value?: boolean | null) => value == null ? null : value ? 'Evet' : 'Hayır';
const serif = Platform.select({ ios: 'Georgia', android: 'serif', default: 'serif' });
const styles = createThemedStyles(() => ({
  screen:{flex:1,backgroundColor:colors.background},content:{width:'100%',maxWidth:680,alignSelf:'center',paddingTop:Platform.OS==='ios'?56:28,paddingHorizontal:20,paddingBottom:110},header:{flexDirection:'row',alignItems:'center',justifyContent:'space-between'},back:{width:44,height:44,borderRadius:22,alignItems:'center',justifyContent:'center',backgroundColor:colors.lilacSoft},headerSpace:{width:44},headerTitle:{color:colors.text,fontFamily:serif,fontSize:22,fontWeight:'700'},loading:{marginTop:80},errorCard:{alignItems:'center',gap:12,backgroundColor:colors.card,borderRadius:22,padding:28,marginTop:30},errorText:{color:colors.danger,textAlign:'center'},profileCard:{alignItems:'center',backgroundColor:colors.card,borderRadius:26,padding:24,marginTop:22,borderWidth:1,borderColor:colors.border,...shadow},avatar:{width:104,height:104,borderRadius:52,backgroundColor:colors.lilacSoft},username:{color:colors.text,fontFamily:serif,fontSize:25,fontWeight:'700',marginTop:13},bio:{color:colors.muted,fontSize:12,lineHeight:19,textAlign:'center',marginTop:9},sectionTitle:{color:colors.text,fontFamily:serif,fontSize:20,fontWeight:'700',marginTop:24,marginBottom:10},empty:{color:colors.muted,textAlign:'center',padding:24},petCard:{flexDirection:'row',alignItems:'center',gap:13,backgroundColor:colors.card,borderRadius:20,padding:13,marginBottom:11,borderWidth:1,borderColor:colors.border},petImage:{width:70,height:70,borderRadius:20,backgroundColor:colors.lilacSoft},flex:{flex:1},nameRow:{flexDirection:'row',alignItems:'center',gap:7},petName:{color:colors.text,fontSize:16,fontWeight:'900'},matchBadge:{color:colors.primary,backgroundColor:colors.lilacSoft,borderRadius:9,paddingHorizontal:6,paddingVertical:3,fontSize:8,fontWeight:'800'},petMeta:{color:colors.muted,fontSize:9,marginTop:3},petAbout:{color:colors.text,fontSize:10,lineHeight:15,marginTop:6},heroImage:{width:'100%',aspectRatio:1.25,borderRadius:26,backgroundColor:colors.lilacSoft,marginTop:22},detailName:{color:colors.text,fontFamily:serif,fontSize:29,fontWeight:'700',marginTop:17},detailMeta:{color:colors.muted,fontSize:11,marginTop:4},aboutCard:{backgroundColor:colors.card,borderRadius:20,padding:17,marginTop:17},detailText:{color:colors.text,fontSize:12,lineHeight:19},fact:{flexDirection:'row',justifyContent:'space-between',gap:15,backgroundColor:colors.card,borderRadius:15,padding:14,marginTop:8},factLabel:{color:colors.muted,fontSize:10},factValue:{flex:1,color:colors.text,fontSize:10,fontWeight:'800',textAlign:'right'},tags:{flexDirection:'row',flexWrap:'wrap',gap:7,marginTop:15},tag:{color:colors.primary,backgroundColor:colors.lilacSoft,borderRadius:12,paddingHorizontal:9,paddingVertical:6,fontSize:9,fontWeight:'800'},
}));
