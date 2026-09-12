import { Ionicons } from '@expo/vector-icons';
import { StatusBar } from 'expo-status-bar';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Alert, Image, KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { getPatiMatchContactShare, getPatiMatchMessages, mediaUrl, revokePatiMatchContact, sendPatiMatchMessage, sharePatiMatchContact, type PatiMatchCandidate, type PatiMatchContactShareState, type PatiMatchMessage, type PatiMatchMyPet } from '../api';
import { colors, createThemedStyles, getThemeMode, shadow } from '../theme';

type Props = {
  token: string;
  sourcePet: PatiMatchMyPet;
  match: PatiMatchCandidate;
  onBack: () => void;
  onOpenProfile: () => void;
};

export function PatiMatchChatScreen({ token, sourcePet, match, onBack, onOpenProfile }: Props) {
  const [messages, setMessages] = useState<PatiMatchMessage[]>([]);
  const [draft, setDraft] = useState('');
  const [loading, setLoading] = useState(true);
  const [sending, setSending] = useState(false);
  const [sharing, setSharing] = useState(false);
  const [contactShare, setContactShare] = useState<PatiMatchContactShareState>({ myStatus: 'none', mine: null, peer: null });
  const [error, setError] = useState<string | null>(null);
  const scrollRef = useRef<ScrollView>(null);

  const load = useCallback(async (quiet = false) => {
    const controller = new AbortController();
    if (!quiet) setLoading(true);
    try {
      const [messagesResult, shareResult] = await Promise.allSettled([
        getPatiMatchMessages(token, sourcePet.id, match.petId, controller.signal),
        getPatiMatchContactShare(token, sourcePet.id, match.petId, controller.signal),
      ]);
      if (messagesResult.status === 'rejected') throw messagesResult.reason;
      setMessages(messagesResult.value);
      if (shareResult.status === 'fulfilled') setContactShare(shareResult.value);
      else if (!quiet) setError(shareResult.reason instanceof Error ? shareResult.reason.message : 'İletişim paylaşımı alınamadı.');
      if (shareResult.status === 'fulfilled') setError(null);
    } catch (reason) {
      if (!quiet) setError(reason instanceof Error ? reason.message : 'Sohbet açılamadı.');
    } finally {
      if (!quiet) setLoading(false);
    }
    return () => controller.abort();
  }, [match.petId, sourcePet.id, token]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    const timer = setInterval(() => void load(true), 5000);
    return () => clearInterval(timer);
  }, [load]);
  useEffect(() => {
    if (messages.length) setTimeout(() => scrollRef.current?.scrollToEnd({ animated: true }), 80);
  }, [messages.length]);

  const send = async () => {
    const body = draft.trim();
    if (!body || sending) return;
    setSending(true);
    try {
      const saved = await sendPatiMatchMessage(token, sourcePet.id, match.petId, body);
      setMessages(current => [...current, saved]);
      setDraft('');
      setError(null);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Mesaj gönderilemedi.');
    } finally {
      setSending(false);
    }
  };

  const shareContact = () => Alert.alert(
    'İletişim bilgilerini paylaş',
    'Telefon/e-posta bilgilerin bu eşleşme ile paylaşılacak.',
    [{ text: 'Vazgeç', style: 'cancel' }, { text: 'Paylaş', onPress: async () => {
      setSharing(true);
      try {
        setContactShare(await sharePatiMatchContact(token, sourcePet.id, match.petId));
      } catch (reason) {
        Alert.alert('Paylaşılamadı', reason instanceof Error ? reason.message : 'İletişim bilgileri paylaşılamadı.');
      } finally { setSharing(false); }
    }}],
  );

  const revokeContact = () => Alert.alert(
    'Paylaşımı geri al',
    'Karşı taraf bilgilerini artık görüntüleyemez. Daha önce kopyalanan bilgiler teknik olarak geri alınamaz.',
    [{ text: 'Vazgeç', style: 'cancel' }, { text: 'Geri al', style: 'destructive', onPress: async () => {
      setSharing(true);
      try {
        setContactShare(await revokePatiMatchContact(token, sourcePet.id, match.petId));
      } catch (reason) {
        Alert.alert('Geri alınamadı', reason instanceof Error ? reason.message : 'Paylaşım geri alınamadı.');
      } finally { setSharing(false); }
    }}],
  );

  return <KeyboardAvoidingView style={styles.screen} behavior={Platform.OS === 'ios' ? 'padding' : undefined} keyboardVerticalOffset={8}>
    <StatusBar style={getThemeMode() === 'dark' ? 'light' : 'dark'} />
    <View style={styles.header}>
      <Pressable onPress={onBack} accessibilityRole="button" accessibilityLabel="Eşleşmelere dön" style={styles.backButton}>
        <Ionicons name="arrow-back" size={23} color={colors.primary} />
      </Pressable>
      <Pressable onPress={onOpenProfile} accessibilityRole="button" accessibilityLabel="Eşleşen profili aç"><Image source={{ uri: mediaUrl(match.profileImage) }} style={styles.avatar} /></Pressable>
      <Pressable onPress={onOpenProfile} style={styles.headerCopy}>
        <Text style={styles.title}>{match.name}</Text>
        <Text style={styles.subtitle}>@{match.ownerUsername} · {sourcePet.name} ile PatiMatch</Text>
      </Pressable>
      <Ionicons name="shield-checkmark-outline" size={23} color="#4E7458" />
    </View>

    <View style={styles.safetyBanner}>
      <Ionicons name="information-circle-outline" size={19} color="#4E7458" />
      <Text style={styles.safetyText}>İletişim bilgilerini yalnızca hazır olduğunda güvenli paylaşım düğmesiyle paylaş. İlk görüşmeyi halka açık bir yerde yap.</Text>
    </View>

    <View style={styles.contactArea}>
      {contactShare?.peer ? <View style={styles.contactCard}>
        <View style={styles.contactTitleRow}><Ionicons name="person-circle-outline" size={20} color={colors.primary} /><Text style={styles.contactTitle}>@{contactShare.peer.username} iletişim bilgilerini paylaştı</Text></View>
        {contactShare.peer.phone ? <Text selectable style={styles.contactValue}>Telefon: {contactShare.peer.phone}</Text> : null}
        {contactShare.peer.email ? <Text selectable style={styles.contactValue}>E-posta: {contactShare.peer.email}</Text> : null}
      </View> : null}
      {contactShare?.myStatus === 'none' ? <Pressable disabled={sharing} onPress={shareContact} style={[styles.contactButton, sharing && styles.disabled]}>
        {sharing ? <ActivityIndicator color={colors.primary} /> : <Ionicons name="share-social-outline" size={18} color={colors.primary} />}
        <Text style={styles.contactButtonText}>İletişim bilgilerimi paylaş</Text>
      </Pressable> : null}
      {contactShare?.myStatus === 'shared' ? <View style={styles.myShareRow}><Text style={styles.myShareText}>İletişim bilgilerin bu eşleşmeyle paylaşılıyor.</Text><Pressable disabled={sharing} onPress={revokeContact}><Text style={styles.revokeText}>Geri al</Text></Pressable></View> : null}
      {contactShare?.myStatus === 'revoked' ? <Text style={styles.revokedText}>İletişim paylaşımını geri aldın. Tek seferlik paylaşım yeniden açılamaz.</Text> : null}
      {contactShare?.myStatus === 'shared' ? <Text style={styles.copyNote}>Geri alma yeni görüntülemeyi engeller; daha önce kopyalanan bilgileri geri alamaz.</Text> : null}
    </View>

    <ScrollView ref={scrollRef} style={styles.messages} contentContainerStyle={styles.messageContent} keyboardShouldPersistTaps="handled">
      {loading ? <ActivityIndicator color={colors.primary} size="large" /> : null}
      {!loading && !messages.length && !error ? <View style={styles.empty}>
        <Ionicons name="chatbubbles-outline" size={40} color={colors.primary} />
        <Text style={styles.emptyTitle}>İlk mesajı sen gönder</Text>
        <Text style={styles.emptyText}>{sourcePet.name} ve {match.name} için güvenli bir tanışma başlatabilirsin.</Text>
      </View> : null}
      {messages.map(message => <View key={message.id} style={[styles.messageRow, message.isMine && styles.messageRowMine]}>
        <View style={[styles.bubble, message.isMine ? styles.bubbleMine : styles.bubbleOther]}>
          {!message.isMine ? <Text style={styles.sender}>{message.username}</Text> : null}
          <Text style={[styles.body, message.isMine && styles.bodyMine]}>{message.body}</Text>
          <Text style={[styles.time, message.isMine && styles.timeMine]}>{formatTime(message.createdAt)}</Text>
        </View>
      </View>)}
    </ScrollView>

    {error ? <Text style={styles.error}>{error}</Text> : null}
    <View style={styles.composer}>
      <TextInput
        value={draft}
        onChangeText={setDraft}
        placeholder="Mesajını yaz…"
        placeholderTextColor={colors.muted}
        style={styles.input}
        multiline
        maxLength={1000}
        accessibilityLabel="Mesaj"
      />
      <Pressable disabled={!draft.trim() || sending} onPress={() => void send()} accessibilityRole="button" accessibilityLabel="Mesajı gönder" style={[styles.sendButton, (!draft.trim() || sending) && styles.disabled]}>
        {sending ? <ActivityIndicator size="small" color={colors.white} /> : <Ionicons name="send" size={20} color={colors.white} />}
      </Pressable>
    </View>
  </KeyboardAvoidingView>;
}

function formatTime(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '' : date.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
}

const styles = createThemedStyles(() => ({
  screen:{flex:1,backgroundColor:colors.background,paddingTop:Platform.OS==='ios'?54:24},
  header:{minHeight:66,flexDirection:'row',alignItems:'center',gap:11,paddingHorizontal:18,borderBottomWidth:1,borderBottomColor:colors.border,backgroundColor:colors.card},
  backButton:{width:38,height:38,borderRadius:19,alignItems:'center',justifyContent:'center',backgroundColor:colors.lilacSoft},
  avatar:{width:43,height:43,borderRadius:15,backgroundColor:colors.lilacSoft},headerCopy:{flex:1},
  title:{color:colors.text,fontSize:16,fontWeight:'900'},subtitle:{color:colors.muted,fontSize:9,marginTop:2},
  safetyBanner:{flexDirection:'row',alignItems:'flex-start',gap:8,backgroundColor:colors.sageSoft,paddingHorizontal:18,paddingVertical:11},
  safetyText:{flex:1,color:'#4E7458',fontSize:9,lineHeight:14},messages:{flex:1},messageContent:{padding:18,paddingBottom:25},
  contactArea:{paddingHorizontal:14,paddingVertical:10,gap:7,backgroundColor:colors.background,borderBottomWidth:1,borderBottomColor:colors.border},
  contactCard:{borderWidth:1,borderColor:colors.border,borderRadius:14,backgroundColor:colors.card,padding:11,gap:4},
  contactTitleRow:{flexDirection:'row',alignItems:'center',gap:6},contactTitle:{flex:1,color:colors.text,fontSize:10,fontWeight:'900'},contactValue:{color:colors.primary,fontSize:10,fontWeight:'800'},
  contactButton:{minHeight:42,borderRadius:14,borderWidth:1,borderColor:colors.primary,flexDirection:'row',alignItems:'center',justifyContent:'center',gap:7,backgroundColor:colors.card},contactButtonText:{color:colors.primary,fontSize:10,fontWeight:'900'},
  myShareRow:{flexDirection:'row',alignItems:'center',justifyContent:'space-between',gap:8},myShareText:{flex:1,color:colors.text,fontSize:9,fontWeight:'700'},revokeText:{color:colors.danger,fontSize:10,fontWeight:'900'},copyNote:{color:colors.muted,fontSize:8,lineHeight:12},revokedText:{color:colors.muted,fontSize:9,lineHeight:14,textAlign:'center'},
  empty:{alignItems:'center',paddingTop:70,paddingHorizontal:28},emptyTitle:{color:colors.text,fontSize:18,fontWeight:'900',marginTop:12},emptyText:{color:colors.muted,fontSize:11,lineHeight:17,textAlign:'center',marginTop:6},
  messageRow:{alignItems:'flex-start',marginBottom:9},messageRowMine:{alignItems:'flex-end'},bubble:{maxWidth:'82%',borderRadius:18,paddingHorizontal:13,paddingVertical:10,...shadow},
  bubbleOther:{backgroundColor:colors.card,borderWidth:1,borderColor:colors.border,borderBottomLeftRadius:5},bubbleMine:{backgroundColor:colors.primary,borderBottomRightRadius:5},
  sender:{color:colors.primary,fontSize:8,fontWeight:'900',marginBottom:3},body:{color:colors.text,fontSize:12,lineHeight:18},bodyMine:{color:colors.white},
  time:{color:colors.muted,fontSize:7,textAlign:'right',marginTop:4},timeMine:{color:'#E8DAE5'},error:{color:colors.danger,fontSize:9,textAlign:'center',paddingHorizontal:18,paddingVertical:5},
  composer:{flexDirection:'row',alignItems:'flex-end',gap:9,paddingHorizontal:14,paddingTop:10,paddingBottom:Platform.OS==='ios'?34:14,backgroundColor:colors.card,borderTopWidth:1,borderTopColor:colors.border},
  input:{flex:1,maxHeight:110,minHeight:45,borderRadius:18,borderWidth:1,borderColor:colors.border,backgroundColor:colors.background,paddingHorizontal:14,paddingVertical:12,color:colors.text,fontSize:12},
  sendButton:{width:45,height:45,borderRadius:17,alignItems:'center',justifyContent:'center',backgroundColor:colors.primary},disabled:{opacity:.4},
}));
